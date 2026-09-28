using UnityEngine;

namespace Jabel.Core
{
    /// <summary>
    /// Base class for scene components that need the running clicker session.
    /// <see cref="OnBind"/> is called once the session is ready (after loading), <see cref="OnUnbind"/>
    /// when the component is disabled. Subscribe to events in OnBind, unsubscribe in OnUnbind.
    /// </summary>
    public abstract class JabelBehaviour : MonoBehaviour
    {
        protected ClickerManager Manager { get; private set; }
        protected bool IsBound { get; private set; }

        protected virtual void OnEnable() => TryBind();

        protected virtual void Start() => TryBind();

        private void TryBind()
        {
            if (IsBound) return;
            var manager = ClickerManager.Instance;
            if (manager == null) return;
            manager.WhenReady(Bind);
        }

        private void Bind()
        {
            if (IsBound || !isActiveAndEnabled || ClickerManager.Instance == null) return;
            Manager = ClickerManager.Instance;
            IsBound = true;
            OnBind();
        }

        protected virtual void OnDisable()
        {
            if (!IsBound) return;
            IsBound = false;
            // Always unsubscribe, even when the manager is already destroyed (scene unload / leaving play mode):
            // the event bus is a plain C# object and static events (localization) outlive the scene.
            // Skipping this left dead listeners behind that threw MissingReferenceException later.
            if (ReferenceEquals(Manager, null)) return;
            try
            {
                OnUnbind();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, this);
            }
        }

        protected virtual void OnBind() { }
        protected virtual void OnUnbind() { }
    }
}
