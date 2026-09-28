using System.Collections.Generic;
using UnityEngine;

namespace Jabel.Scripting
{
    /// <summary>
    /// A reusable script stored as an asset: write "Give welcome bonus" once, run it from anywhere
    /// with the "Run script asset" block.
    /// </summary>
    [CreateAssetMenu(menuName = "Jabel/Script Asset", fileName = "JabelScript")]
    public class JabelScriptAsset : ScriptableObject
    {
        [TextArea(2, 4)]
        [SerializeField] private string description;
        [Tooltip("Documentation only: names of locals this script expects as arguments.")]
        [SerializeField] private List<string> parameters = new List<string>();
        [SerializeField] private JabelScript script = new JabelScript();

        public JabelScript Script => script;
        public IReadOnlyList<string> Parameters => parameters;
    }
}
