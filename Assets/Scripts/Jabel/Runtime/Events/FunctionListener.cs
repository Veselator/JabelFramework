using Jabel.Core;
using Jabel.Scripting;
using UnityEngine;
using UnityEngine.Events;

namespace Jabel.Events
{
    /// <summary>
    /// Reacts in the scene when a script calls a function: e.g. the "ShakeCamera" function of a
    /// "Call function" block triggers a UnityEvent here. The first argument is passed as float.
    /// </summary>
    [AddComponentMenu("Jabel/Events/Function Listener")]
    public class FunctionListener : JabelBehaviour
    {
        [SerializeField] private string function = "MyFunction";
        [SerializeField] private bool ignoreOffline = true;
        [SerializeField] private UnityEvent<float> onCalled = new UnityEvent<float>();
        [Tooltip("Runs with the call's arguments as locals.")]
        [SerializeField] private JabelScript response = new JabelScript();

        protected override void OnBind() => Manager.Events.Subscribe<FunctionCalledEvent>(OnFunction);

        protected override void OnUnbind() => Manager.Events.Unsubscribe<FunctionCalledEvent>(OnFunction);

        private void OnFunction(FunctionCalledEvent evt)
        {
            if (evt.Name != function) return;
            if (ignoreOffline && evt.Context != null && evt.Context.IsOffline) return;

            onCalled.Invoke((float)evt.Args.First().ToDoubleClamped());

            if (response.IsEmpty) return;
            var ctx = evt.Context != null ? evt.Context.CreateChild() : JabelContext.Rent(Manager);
            try
            {
                for (int i = 0; i < evt.Args.Count; i++) ctx.SetLocal(evt.Args.NameAt(i), evt.Args.ValueAt(i));
                response.Run(ctx);
            }
            finally
            {
                ctx.Release();
            }
        }
    }
}
