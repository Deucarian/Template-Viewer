using System;
using UnityEngine;

namespace Deucarian.TemplateViewer
{
    [DisallowMultipleComponent]
    internal sealed class ViewerModelRevealLifecycleRelay : MonoBehaviour
    {
        public event Action Interrupted;

        private void OnDisable()
        {
            Interrupted?.Invoke();
        }

        private void OnDestroy()
        {
            Interrupted?.Invoke();
        }
    }
}
