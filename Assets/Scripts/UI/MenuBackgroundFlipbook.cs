using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays an ambient animation loop on the menu background, if frames exist at
/// Resources/UI/Menu/background_frames (bg frames named so they sort in order).
/// Crossfade-free simple flipbook at low fps; sits under MenuCinematicBackground's
/// Ken Burns drift, so the result is a living, slowly-drifting scene.
/// Does nothing (and disables itself) when no frames are present.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class MenuBackgroundFlipbook : MonoBehaviour
{
    public float fps = 6f;

    Image _image;
    Sprite[] _frames;
    float _timer;
    int _index;

    void Awake()
    {
        _image = GetComponent<Image>();
        _frames = Resources.LoadAll<Sprite>("UI/Menu/background_frames");
        if (_frames == null || _frames.Length < 2)
        {
            enabled = false;
            return;
        }
        System.Array.Sort(_frames, (a, b) =>
            string.CompareOrdinal(a.name, b.name));
        _image.sprite = _frames[0];
    }

    void Update()
    {
        _timer += Time.unscaledDeltaTime;
        float spf = 1f / Mathf.Max(1f, fps);
        while (_timer >= spf)
        {
            _timer -= spf;
            _index = (_index + 1) % _frames.Length;
        }
        _image.sprite = _frames[_index];
    }
}
