﻿using System.Collections.Generic;
using Travis.Games.GreedyNumbers;
using Travis.Logic.Learning;
using Travis.Logic.Learning.Model;
using Travis.Logic.MCTS;

namespace Travis.TreeBuilder
{
    /// <summary>
    /// Class which contains program entry point.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Program entry point.
        /// </summary>
        public static void Main()
        {
            var processor = new TreeSearchProcessor();
            var tree = new TreeNode();
            var game = new GreedyNumbers(2, new Dictionary<int, int>() { { 1, 5 }, { 2, 3 }, { 7, 1 } });
            processor.Process(tree, game, 1000, MCTSActionSelector.Create(game.EnumerateActors()));
        }
    }
}
