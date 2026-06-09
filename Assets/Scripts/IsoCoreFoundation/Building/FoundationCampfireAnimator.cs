using UnityEngine;

namespace IsoCore.Foundation
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FoundationCampfireAnimator : MonoBehaviour
    {
        public float framesPerSecond = 8f;

        SpriteRenderer _renderer;
        Sprite[] _frames;
        int _index;
        float _timer;
        Vector3 _baseLocalPosition;
        float _baseFrameBottom;
        bool _anchored;

        public void Init(SpriteRenderer renderer = null)
        {
            _renderer = renderer != null ? renderer : GetComponent<SpriteRenderer>();
            _frames = FoundationPlaceableSpriteResolver.CampfireFrames();
            if (_frames != null && _frames.Length > 0)
            {
                _baseLocalPosition = transform.localPosition;
                _baseFrameBottom = _frames[0].bounds.min.y;
                _anchored = true;
                ApplyFrame(0);
            }
        }

        void Awake()
        {
            if (_renderer == null)
                Init();
        }

        void Update()
        {
            if (_renderer == null || _frames == null || _frames.Length <= 1)
                return;

            _timer += Time.deltaTime;
            float step = 1f / Mathf.Max(1f, framesPerSecond);
            while (_timer >= step)
            {
                _timer -= step;
                _index = (_index + 1) % _frames.Length;
                ApplyFrame(_index);
            }
        }

        void ApplyFrame(int index)
        {
            if (_renderer == null || _frames == null || _frames.Length == 0)
                return;

            index = Mathf.Clamp(index, 0, _frames.Length - 1);
            var frame = _frames[index];
            _renderer.sprite = frame;

            if (!_anchored || frame == null)
                return;

            float yOffset = (_baseFrameBottom - frame.bounds.min.y) * transform.localScale.y;
            transform.localPosition = _baseLocalPosition + new Vector3(0f, yOffset, 0f);
        }
    }
}
