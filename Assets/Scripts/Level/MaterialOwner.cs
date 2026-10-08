using UnityEngine;

namespace Warehouse.Levels
{
    /// <summary>Destroys a runtime-instanced material when the object is removed.</summary>
    public class MaterialOwner : MonoBehaviour
    {
        public Material material;

        private void OnDestroy()
        {
            if (material != null)
                Destroy(material);
        }
    }
}
