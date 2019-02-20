using UnityEngine;
using System;
using System.Collections.Generic;

public class Grid<T>
{
    public T[] data { get { return _data; } }
    public int width { get { return _width; } }
    public int height { get { return _height; } }
    public float tileSize { get { return _tileSize; } }

    T[] _data;
    int _width;
    int _height;
    float _tileSize;

    public class TileInfo
    {
        public int x;
        public int y;
        public T value;
    }

    public Grid(Vector3 size, float ts)
    {
        _width = (int)Math.Ceiling(size.x / ts);
        _height = (int)Math.Ceiling(size.z / ts);

        _data = new T[_width * _height];
        _tileSize = ts;
    }

    public Grid(T[] d, float ts, int w, int h)
    {
        _data = d;
        _tileSize = ts;
        _width = w;
        _height = h;
    }

    public T GetValue(int x, int y)
    {
        Debug.Assert(x >= 0 && y >= 0 && x < _width && y < _height, "Grid error x " + x + " y " + y);
        return _data[y * _width + x];
    }

    public T GetValue(Vector3 pos)
    {
        return GetValue(Mathf.FloorToInt(pos.x / _tileSize), Mathf.FloorToInt(pos.z / _tileSize));
    }

    public TileInfo GetTileInfo(int x, int y)
    {
        Debug.Assert(x >= 0 && y >= 0 && x < _width && y < _height, "Grid error x " + x + " y " + y);
        return new TileInfo { x = x, y = y, value = _data[y * _width + x] };
    }

    public TileInfo GetTileInfo(Vector3 pos)
    {
        return GetTileInfo(Mathf.FloorToInt(pos.x / _tileSize), Mathf.FloorToInt(pos.z / _tileSize));
    }

    public Vector3 GetTilePos(int x, int y)
    {
        return new Vector3((x + 0.5f) * _tileSize, 0f, (y + 0.5f) * _tileSize);
    }

    public Vector3 GetTilePos(TileInfo tile)
    {
        return GetTilePos(tile.x, tile.y);
    }

    public List<TileInfo> SetPatch(Vector3 pos, Vector3 dir, Vector3 size, T value)
    {
        // Create rect
        Vector3 hs = new Vector3(size.x * 0.5f, 0, size.z * 0.5f);
        Vector3 lt = new Vector3(-hs.x, 0, -hs.z);
        Vector3 rt = new Vector3(hs.x, 0, -hs.z);
        Vector3 rb = new Vector3(hs.x, 0, hs.z);
        Vector3 lb = new Vector3(-hs.x, 0, hs.z);

        // Rotate rect
        Vector3 ltRot = RotatePointXZ(lt, dir.y) + pos;
        Vector3 rtRot = RotatePointXZ(rt, dir.y) + pos;
        Vector3 rbRot = RotatePointXZ(rb, dir.y) + pos;
        Vector3 lbRot = RotatePointXZ(lb, dir.y) + pos;

        // Get min-max rect
        Vector3 min = new Vector3(ltRot.x, ltRot.y, ltRot.z);
        Vector3 max = new Vector3(ltRot.x, ltRot.y, ltRot.z);
        Vector3[] vertices = { rtRot, rbRot, lbRot };
        foreach (Vector3 vert in vertices)
        {
            min.x = Mathf.Min(min.x, vert.x);
            min.z = Mathf.Min(min.z, vert.z);
            max.x = Mathf.Max(max.x, vert.x);
            max.z = Mathf.Max(max.z, vert.z);
        }

        // Iteration limits
        TileInfo minTile = new TileInfo { x = Mathf.FloorToInt(min.x / _tileSize + 0.5f), y = Mathf.FloorToInt(min.z / _tileSize + 0.5f) };
        TileInfo maxTile = new TileInfo { x = Mathf.FloorToInt(max.x / _tileSize - 0.5f), y = Mathf.FloorToInt(max.z / _tileSize - 0.5f) };

        minTile.x = minTile.x < 0 ? 0 : minTile.x;
        minTile.y = minTile.y < 0 ? 0 : minTile.y;
        maxTile.x = maxTile.x >= _width ? _width - 1 : maxTile.x;
        maxTile.y = maxTile.y >= _height ? _height - 1 : maxTile.y;

        Bounds originalRect = new Bounds(pos, size);
        List<TileInfo> patch = new List<TileInfo>();

        for (int y = minTile.y; y <= maxTile.y; y++)
        {
            for (int x = minTile.x; x <= maxTile.x; x++)
            {
                Vector3 p = new Vector3((x + 0.5f) * _tileSize, 0.0f, (y + 0.5f) * _tileSize) - pos;
                p = RotatePointXZ(p, -dir.y) + pos;
                if (originalRect.Contains(p))
                {
                    _data[y * _width + x] = value;
                    patch.Add(new TileInfo { x = x, y = y, value = value });
                }
            }
        }

        return patch;
    }

    Vector3 RotatePointXZ(Vector3 p, float deg)
    {
        float rad = -deg / 180.0f * Mathf.PI;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector3(cos * p.x - sin * p.z, 0, sin * p.x + cos * p.z);
    }
}
