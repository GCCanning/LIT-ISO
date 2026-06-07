using System;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Runtime instance pockets for building interiors and portals. This keeps the
    /// canonical scene and grid intact while giving entrances a real teleport target.
    /// </summary>
    public sealed class FoundationInstanceSystem
    {
        IsoWorld _world;
        IsoFoundationPlayer _player;
        FoundationContent _content;
        FoundationInteractionOverlay _overlay;

        bool _inside;
        string _instanceId;
        string _displayName;
        Vector2 _returnGround;
        Vector2Int _returnCell;
        Vector2Int _originCell;

        const int PocketHalfSize = 4;

        public bool IsInsideInstance => _inside;
        public string ActiveInstanceId => _inside ? _instanceId : "";
        public string ActiveDisplayName => _inside ? _displayName : "";
        public Vector2Int OriginCell => _originCell;
        public Vector2Int ReturnCell => _returnCell;

        public event Action<string, string> Entered;
        public event Action<string, string> Exited;

        public void Init(IsoWorld world, IsoFoundationPlayer player, FoundationContent content,
            FoundationInteractionOverlay overlay)
        {
            _world = world;
            _player = player;
            _content = content;
            _overlay = overlay;
        }

        public bool Enter(PlaceableDefinition entrance, Vector2Int entranceCell)
        {
            if (entrance == null || string.IsNullOrWhiteSpace(entrance.destinationId) ||
                _world == null || _player == null || _content == null)
                return false;

            _returnGround = _player.Ground;
            _returnCell = entranceCell;
            _instanceId = entrance.destinationId.Trim();
            _displayName = string.IsNullOrWhiteSpace(entrance.destinationDisplayName)
                ? entrance.Display
                : entrance.destinationDisplayName.Trim();
            _originCell = OriginFor(_instanceId);
            _inside = true;

            BuildPocket(_instanceId, _displayName, _originCell);
            _player.SetCell(_originCell.x, _originCell.y);
            _overlay?.Flash($"Entered {_displayName}");
            Entered?.Invoke(_instanceId, _displayName);
            return true;
        }

        public bool Exit()
        {
            if (!_inside || _player == null) return false;

            string id = _instanceId;
            string display = _displayName;
            _player.SetGround(_returnGround);
            _inside = false;
            _instanceId = "";
            _displayName = "";
            _originCell = default;

            _overlay?.Flash($"Returned from {display}");
            Exited?.Invoke(id, display);
            return true;
        }

        public FoundationSavedInstance CaptureState()
        {
            return new FoundationSavedInstance
            {
                active = _inside,
                instanceId = _instanceId,
                displayName = _displayName,
                originX = _originCell.x,
                originY = _originCell.y,
                returnCellX = _returnCell.x,
                returnCellY = _returnCell.y,
                returnGroundX = _returnGround.x,
                returnGroundY = _returnGround.y,
            };
        }

        public void RestoreState(FoundationSavedInstance state)
        {
            _inside = false;
            _instanceId = "";
            _displayName = "";
            _originCell = default;

            if (!state.active || string.IsNullOrWhiteSpace(state.instanceId) || _world == null)
                return;

            _inside = true;
            _instanceId = state.instanceId.Trim();
            _displayName = string.IsNullOrWhiteSpace(state.displayName) ? _instanceId : state.displayName.Trim();
            _originCell = state.originX != 0 || state.originY != 0
                ? new Vector2Int(state.originX, state.originY)
                : OriginFor(_instanceId);
            _returnCell = new Vector2Int(state.returnCellX, state.returnCellY);
            _returnGround = new Vector2(state.returnGroundX, state.returnGroundY);
            BuildPocket(_instanceId, _displayName, _originCell);
        }

        void BuildPocket(string instanceId, string displayName, Vector2Int origin)
        {
            var floor = _content.Blocks.Get("wood_floor") ?? _content.Blocks.Get("stone_path");
            string floorId = floor != null ? floor.id : "grass_1";
            var cells = new FoundationSavedCell[(PocketHalfSize * 2 + 1) * (PocketHalfSize * 2 + 1)];
            int n = 0;

            for (int y = -PocketHalfSize; y <= PocketHalfSize; y++)
            for (int x = -PocketHalfSize; x <= PocketHalfSize; x++)
            {
                bool wall = Mathf.Abs(x) == PocketHalfSize || Mathf.Abs(y) == PocketHalfSize;
                cells[n++] = new FoundationSavedCell
                {
                    x = origin.x + x,
                    y = origin.y + y,
                    height = 0,
                    biomeIndex = 0,
                    surfaceBlockId = wall ? "stone_path" : floorId,
                    occupantId = null,
                    nodeId = null,
                    solidBlock = false,
                    water = false,
                    occupantBlocks = false,
                    nodeBlocks = false,
                    underBlockId = null,
                    underHeight = 0,
                };
            }

            _world.RestoreModifiedCells(cells);
        }

        static Vector2Int OriginFor(string instanceId)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < instanceId.Length; i++)
                    hash = hash * 31 + instanceId[i];

                int bucketX = Mathf.Abs(hash % 64);
                int bucketY = Mathf.Abs((hash / 64) % 64);
                return new Vector2Int(20000 + bucketX * 32, 20000 + bucketY * 32);
            }
        }
    }
}
