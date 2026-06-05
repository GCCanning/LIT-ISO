using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class HideTilemapColliderOnPlay : MonoBehaviour
{

    private TilemapRenderer tilemapRenderer;
    private TilemapCollider2D tilemapCollider;

    void Start()
    {
        tilemapRenderer = GetComponent<TilemapRenderer>();
        tilemapCollider = GetComponent<TilemapCollider2D>();

        if (tilemapCollider != null)
        {
            tilemapCollider.enabled = false;
            return;
        }

        if (tilemapRenderer != null)
        {
            tilemapRenderer.enabled = false;
        }
    }
}
