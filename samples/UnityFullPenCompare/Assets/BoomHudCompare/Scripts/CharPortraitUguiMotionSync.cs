using Generated.Hud.UGui;
using UnityEngine;

namespace BoomHud.Compare
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CharPortraitUguiMotionSync : MonoBehaviour
    {
        [SerializeField] private CharPortraitMotionHost? _motionHost;

        private void OnEnable()
        {
            Subscribe();
            TrySync();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Reset()
        {
            _motionHost = GetComponent<CharPortraitMotionHost>();
        }

        private void OnValidate()
        {
            if (_motionHost == null)
            {
                _motionHost = GetComponent<CharPortraitMotionHost>();
            }

            TrySync();
        }

        private void Subscribe()
        {
            if (_motionHost == null)
            {
                _motionHost = GetComponent<CharPortraitMotionHost>();
            }

            if (_motionHost == null)
            {
                return;
            }

            _motionHost.MotionApplied -= HandleMotionApplied;
            _motionHost.MotionApplied += HandleMotionApplied;
        }

        private void Unsubscribe()
        {
            if (_motionHost == null)
            {
                return;
            }

            _motionHost.MotionApplied -= HandleMotionApplied;
        }

        private void HandleMotionApplied(string clipId, float timeSeconds)
        {
            TrySync();
        }

        private void TrySync()
        {
            if (_motionHost == null)
            {
                return;
            }

            try
            {
                UGuiHudPreviewComposer.SyncComposedCharPortraitBars(_motionHost.View);
            }
            catch (System.InvalidOperationException)
            {
            }
        }
    }
}
