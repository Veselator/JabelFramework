using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jabel.Scripting
{
    public enum BlockResult
    {
        Continue,
        /// <summary>Leave the innermost loop.</summary>
        Break,
        /// <summary>Stop the whole script.</summary>
        Return
    }

    /// <summary>Metadata shown in the block picker: menu path, header color and help text.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class JabelBlockAttribute : Attribute
    {
        public string Path { get; }
        public string Color { get; }
        public string Help { get; set; }

        public JabelBlockAttribute(string path, string color = "#5B6B7A")
        {
            Path = path;
            Color = color;
        }
    }

    /// <summary>
    /// One statement of JabelScript. Blocks are plain serializable classes stored with
    /// [SerializeReference], so new block types are added by simply writing a subclass —
    /// the editor picks them up automatically (open/closed principle).
    /// </summary>
    [Serializable]
    public abstract class JabelBlock
    {
        [SerializeField, HideInInspector] private bool enabled = true;

        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        public abstract BlockResult Execute(JabelContext context);

        /// <summary>One-line human readable summary shown on collapsed blocks.</summary>
        public virtual string Describe() => GetType().Name.Replace("Block", string.Empty);
    }

    /// <summary>
    /// A list of blocks executed top to bottom. Used for every hook in the framework:
    /// OnStart, OnClick, OnBought, OnTick, OnLevelUp...
    /// </summary>
    [Serializable]
    public class JabelScript
    {
        [SerializeReference] private List<JabelBlock> blocks = new List<JabelBlock>();

        public IReadOnlyList<JabelBlock> Blocks => blocks;
        public bool IsEmpty => blocks == null || blocks.Count == 0;

        public JabelScript() { }

        public JabelScript(params JabelBlock[] initial)
        {
            blocks = new List<JabelBlock>(initial);
        }

        public JabelScript Add(JabelBlock block)
        {
            blocks ??= new List<JabelBlock>();
            blocks.Add(block);
            return this;
        }

        /// <summary>Runs the blocks inside an existing context (locals are shared with the caller).</summary>
        public BlockResult Run(JabelContext context)
        {
            if (IsEmpty || context == null) return BlockResult.Continue;
            if (context.Depth > JabelContext.MaxDepth)
            {
                Debug.LogError("[Jabel] Script recursion limit reached. Does a function call itself?");
                return BlockResult.Return;
            }

            context.Depth++;
            try
            {
                for (int i = 0; i < blocks.Count; i++)
                {
                    var block = blocks[i];
                    if (block == null || !block.Enabled) continue;
                    var result = block.Execute(context);
                    if (result != BlockResult.Continue) return result;
                }
                return BlockResult.Continue;
            }
            finally
            {
                context.Depth--;
            }
        }

        /// <summary>Convenience: rents a context, lets the caller fill it, runs, releases.</summary>
        public void Execute(IJabelRuntime runtime, Action<JabelContext> setup = null)
        {
            if (IsEmpty || runtime == null) return;
            var context = JabelContext.Rent(runtime);
            try
            {
                setup?.Invoke(context);
                Run(context);
            }
            finally
            {
                context.Release();
            }
        }
    }
}
