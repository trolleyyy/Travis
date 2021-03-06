﻿using Travis.Logic.Model;

namespace Travis.Logic.Learning.Model
{
    /// <summary>
    /// Represents tree policy.
    /// </summary>
    public interface ITreePolicy
    {
        /// <summary>
        /// Selects action for particular actor for given state within game tree.
        /// </summary>
        /// <param name="node">TreeNode refering to <paramref name="state"/>.</param>
        /// <param name="state">A state of game.</param>
        /// <param name="actorId">Actor id action should be selected for.</param>
        IAction Invoke(TreeNode node, IState state, int actorId);
    }
}
