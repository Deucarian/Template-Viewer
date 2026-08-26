using UnityEngine;

namespace Deucarian.TemplateViewer.Selection
{
    [DisallowMultipleComponent]
    public sealed class ViewerElement : MonoBehaviour
    {
        [SerializeField] private string elementId;

        public string ElementId =>
            string.IsNullOrWhiteSpace(elementId) ? string.Empty : elementId.Trim();

        public void Initialize(string stableId)
        {
            elementId = string.IsNullOrWhiteSpace(stableId)
                ? string.Empty
                : stableId.Trim();
        }

        private void OnValidate()
        {
            elementId = string.IsNullOrWhiteSpace(elementId)
                ? string.Empty
                : elementId.Trim();
        }
    }
}
