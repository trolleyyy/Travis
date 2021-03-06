﻿using System;
using System.Collections.Generic;
using System.Linq;
using Travis.Logic.Model;
using Travis.Logic.Learning.Model;

namespace Travis.Logic.Learning
{
    /// <summary>
    /// Performs tree learning with MCTS algorithm.
    /// </summary>
    public class TreeSearchProcessor
    {
        /// <summary>
        /// Runs MCTS algorithm on tree for given game.
        /// </summary>
        /// <param name="root">Tree root.</param>
        /// <param name="game">Game to process.</param>
        /// <param name="iterations">Number of iterations of algorithm to run.</param>
        /// <param name="actionSelectors">Action selectors for actors.</param>
        public void Process(TreeNode root, IGame game, int iterations, IDictionary<int, ActionSelector> actionSelectors)
        {
            Process(root, game.GetInitialState(), game, iterations, actionSelectors);
        }

        /// <summary>
        /// Runs MCTS algorithm on tree for given game starting with given state.
        /// </summary>
        /// <param name="root">Tree root.</param>
        /// <param name="rootState">Game state refering to tree root.</param>
        /// <param name="game">Game to process starting at <paramref name="rootState"/>.</param>
        /// <param name="iterations">Number of iterations of algorithm to run.</param>
        /// <param name="actionSelectors">Action selectors for actors.</param>
        public void Process(TreeNode root, IState rootState, IGame game, int iterations, IDictionary<int, ActionSelector> actionSelectors)
        {
            Process(root, rootState, game, new IterationBasedBudgetProvider(iterations), actionSelectors);
        }

        /// <summary>
        /// Runs MCTS algorithm on tree for given game.
        /// </summary>
        /// <param name="root">Tree root.</param>
        /// <param name="game">Game to process.</param>
        /// <param name="computationalBudget">Computational budget to run learning.</param>
        /// <param name="actionSelectors">Action selectors for actors.</param>
        public void Process(TreeNode root, IGame game, IBudgetProvider computationalBudget, IDictionary<int, ActionSelector> actionSelectors)
        {
            Process(root, game.GetInitialState(), game, computationalBudget, actionSelectors);
        }

        /// <summary>
        /// Runs MCTS algorithm on tree for given game starting with given state.
        /// </summary>
        /// <param name="root">Tree root.</param>
        /// <param name="rootState">Problem state refering to tree root.</param>
        /// <param name="game">Game to process starting at <paramref name="rootState"/>.</param>
        /// <param name="computationalBudget">Computational budget to run learning.</param>
        /// <param name="actionSelectors">Action selectors for actors.</param>
        public void Process(TreeNode root, IState rootState, IGame game, IBudgetProvider computationalBudget, IDictionary<int, ActionSelector> actionSelectors)
        {
            computationalBudget.Start();
            while (computationalBudget.HasBudgetLeft())
            {
                ProcessIteration(root, rootState, game, actionSelectors);
                computationalBudget.Next();
            }
        }

        /// <summary>
        /// Runs single interation of MCTS algorithm on given tree.
        /// </summary>
        /// <param name="root">Tree root.</param>
        /// <param name="rootState">State refering to tree root.</param>
        /// <param name="game">Game to process starting at <paramref name="rootState"/>.</param>
        /// <param name="actionSelectors">Action selectors for actors.</param>
        public void ProcessIteration(TreeNode root, IState rootState, IGame game, IDictionary<int, ActionSelector> actionSelectors)
        {
            InitIteration(root, rootState, game, actionSelectors);
            var actionSet = Select();
            Expand(actionSet);
            Simulate();
            Backpropagate();
        }

        #region Common
        private TreeNode currentNode;

        private IState currentState;

        private Stack<Tuple<TreeNode, ActionSet>> decisionPath;

        private IGame game;

        private IDictionary<int, ActionSelector> actionSelectors;

        private void InitIteration(TreeNode root, IState rootState, IGame game, IDictionary<int, ActionSelector> actionSelectors)
        {
            currentNode = root;
            currentState = rootState.Clone();
            decisionPath = new Stack<Tuple<TreeNode, ActionSet>>();
            this.actionSelectors = actionSelectors;
            this.game = game;
            OnStartIteration();
        }

        private void PushDecisionPath(ActionSet actionSet)
        {
            if (currentNode != null)
                decisionPath.Push(Tuple.Create(currentNode, actionSet));
        }

        private void PopDecisionPath(out TreeNode node, out ActionSet actionSet)
        {
            var decisionNode = decisionPath.Pop();
            node = decisionNode.Item1;
            actionSet = decisionNode.Item2;
        }
        #endregion

        #region Selection
        private ActionSet Select()
        {
            while (!currentState.IsTerminal)
            {
                var actionSet = SelectActionsTreePolicy();
                if (currentNode.Children.ContainsKey(actionSet.ActionSetId))
                {
                    ApplyActionSet(actionSet);
                    currentNode = currentNode.Children[actionSet.ActionSetId];
                }
                else return actionSet;
            }
            return null;
        }

        private ActionSet SelectActionsTreePolicy()
        {
            var actions = new Dictionary<int, IAction>();
            foreach (var actorId in game.EnumerateActors())
            {
                var action = actionSelectors[actorId].TreePolicy.Invoke(currentNode, currentState, actorId);
                actions.Add(actorId, action);
            }
            var actionSet = currentState.CreateActionSet(actions);
            return actionSet;
        }
        #endregion

        #region Expansion

        private void Expand(ActionSet actionSet)
        {
            if (actionSet != null)
            {
                ApplyActionSet(actionSet);
                currentNode = currentNode.AddNode(actionSet.ActionSetId, currentState.IsTerminal);
            }
        }

        private void ApplyActionSet(ActionSet actionSet)
        {
            //Console.WriteLine("Selected actions - " + string.Join(", ", actionSet.Actions.Select(kv => $"{kv.Key}: {kv.Value}")));
            OnStateTransition(actionSet);
            PushDecisionPath(actionSet);
            currentState.Apply(actionSet);
            //Console.WriteLine("State " + currentState);
            //if (currentState.IsTerminal)
            //{
            //    var p = currentState.GetPayoffs();
            //    Console.WriteLine($"Payoffs: {p[0]}, {p[1]}");
            //}
        }

        #endregion

        #region Simulation

        private void Simulate()
        {
            while (!currentState.IsTerminal)
            {
                var actionSet = SelectActionsDefaultPolicy();
                ApplyActionSet(actionSet);
                currentNode = null;
            }
            PushDecisionPath(null);
        }

        private ActionSet SelectActionsDefaultPolicy()
        {
            var actions = new Dictionary<int, IAction>();
            foreach (var actorId in game.EnumerateActors())
            {
                var action = actionSelectors[actorId].DefaultPolicy.Invoke(currentState, actorId);
                actions.Add(actorId, action);
            }
            var actionSet = currentState.CreateActionSet(actions);
            return actionSet;
        }

        #endregion

        #region Backpropagation

        private void Backpropagate()
        {
            TreeNode node;
            ActionSet actionSet;

            OnFinishIteration();
            var payoffs = currentState.GetPayoffs();
            while (decisionPath.Any())
            {
                PopDecisionPath(out node, out actionSet);
                node.Quality.NumVisited++;
                if (actionSet != null)
                {
                    foreach (var action in actionSet.Actions.Values)
                    {
                        var actionInfo = GetActionInfo(node, action);
                        var S = actionInfo.Quality;
                        var n = actionInfo.NumSelected;
                        actionInfo.Quality = (n * S + payoffs[action.ActorId]) / (n + 1);
                        actionInfo.NumSelected++;
                    }
                }
            }
        }

        private static ActionQualityInfo GetActionInfo(TreeNode node, IAction action)
        {
            ActorQualityInfo actorInfo;
            if (!node.Quality.ActorActionsQualities.TryGetValue(action.ActorId, out actorInfo))
            {
                actorInfo = new ActorQualityInfo();
                node.Quality.ActorActionsQualities.Add(action.ActorId, actorInfo);
            }
            ActionQualityInfo actionInfo;
            if (!actorInfo.TryGetValue(action.ActionId, out actionInfo))
            {
                actionInfo = new ActionQualityInfo();
                actorInfo.Add(action.ActionId, actionInfo);
            }
            return actionInfo;
        }

        #endregion

        #region Events
        /// <summary>
        /// Event invoked when next algorithm iteration has been started.
        /// </summary>
        public event Action<TreeNode, IState> IterationStarted;

        /// <summary>
        /// Event invoked when game state during iteration has been started.
        /// </summary>
        public event Action<TreeNode, IState, ActionSet> StateTransition;

        /// <summary>
        /// Event invoked when algorithm iteration has been finished.
        /// </summary>
        public event Action<IState> IterationFinished;

        private void OnStartIteration()
        {
            IterationStarted?.Invoke(currentNode, currentState);
        }

        private void OnStateTransition(ActionSet actionSet)
        {
            StateTransition?.Invoke(currentNode, currentState, actionSet);
        }

        private void OnFinishIteration()
        {
            IterationFinished?.Invoke(currentState);
        }
        #endregion
    }
}
