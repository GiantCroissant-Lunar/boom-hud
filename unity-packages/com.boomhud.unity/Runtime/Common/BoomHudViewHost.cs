using UnityEngine;

namespace BoomHud.Unity.Runtime
{
    public abstract class BoomHudViewHost : MonoBehaviour
    {
        [SerializeField] private bool _rebindOnEnable = true;

        protected virtual void Awake()
        {
            EnsureInitialized();
        }

        protected virtual void OnEnable()
        {
            if (_rebindOnEnable)
            {
                Rebind();
            }
        }

        protected virtual void OnDisable()
        {
            Unbind();
        }

        public void Rebind()
        {
            EnsureInitialized();
            Bind();
        }

        protected abstract void EnsureInitialized();

        protected abstract void Bind();

        protected virtual void Unbind()
        {
        }
    }
}