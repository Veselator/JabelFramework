using UnityEngine;

namespace OneKMonkeys
{
    /// <summary>Keeps a background sprite covering the camera view on any aspect ratio.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [ExecuteAlways]
    [AddComponentMenu("1000 Monkeys/Background Fitter")]
    public class BackgroundFitter : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [Tooltip("Extra coverage so camera shake never reveals edges.")]
        [SerializeField] private float margin = 1.05f;

        private SpriteRenderer _renderer;

        private void LateUpdate()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null || _renderer.sprite == null) return;

            float height = targetCamera.orthographicSize * 2f;
            float width = height * targetCamera.aspect;
            var size = _renderer.sprite.bounds.size;
            // The art is uniform horizontally, so width is stretched freely and height is fitted.
            transform.localScale = new Vector3(width / size.x * margin, height / size.y * margin, 1);
        }
    }
}
