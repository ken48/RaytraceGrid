using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    [SerializeField]
    LineRenderer _lineRenderer;
    [SerializeField]
    Vector3 _size;
    [SerializeField]
    float _tileSize;
    [SerializeField]
    float _initCurAngle;

    Grid<bool> _grid;
    float _curAngle;
    List<GameObject> _nodes;
    LineRenderer _obstacleLineRenderer;
    LineRenderer _traceLineRenderer;
    Vector3? _startPoint;
    GameObject _waypointGo;
    List<GameObject> _debugNodes;

    void Start ()
    {
        // Create grid
        _grid = new Grid<bool>(_size, _tileSize);

        // Draw grid
        var positions = new List<Vector3>();
        for (int x = 1; x < _grid.width; x++)
        {
            float z = (x % 2 == 0) ? _size.z : 0f;
            positions.Add(new Vector3(x * _tileSize, 0f, z));
            positions.Add(new Vector3(x * _tileSize, 0f, _size.z - z));
        }

        _nodes = new List<GameObject>();
        _debugNodes = new List<GameObject>();

        positions.Add(Camera.main.ViewportToWorldPoint(new Vector3(0f, 0, _size.z)));

        for (int z = 1; z < _grid.height; z++)
        {
            float x = (z % 2 == 0) ? _size.x : 0f;
            positions.Add(new Vector3(x, 0f, z * _tileSize));
            positions.Add(new Vector3(_size.x - x, 0f, z * _tileSize));
        }

        _lineRenderer.positionCount = positions.Count;
        _lineRenderer.SetPositions(positions.ToArray());

        _obstacleLineRenderer = new GameObject().AddComponent<LineRenderer>();
        _obstacleLineRenderer.loop = true;
        var grad = new Gradient();
        _obstacleLineRenderer.SetWidth(0.01f, 0.01f);
        grad.SetKeys(new [] { new GradientColorKey(Color.magenta, 0f), new GradientColorKey(Color.magenta, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0f) });
        _obstacleLineRenderer.colorGradient = grad;
        _obstacleLineRenderer.positionCount = 4;
        _obstacleLineRenderer.material = _lineRenderer.material;

        _traceLineRenderer = new GameObject().AddComponent<LineRenderer>();
        var gradTrace = new Gradient();
        _traceLineRenderer.SetWidth(0.01f, 0.01f);
        gradTrace.SetKeys(new[] { new GradientColorKey(Color.green, 0f), new GradientColorKey(Color.green, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0f) });
        _traceLineRenderer.colorGradient = gradTrace;
        _traceLineRenderer.positionCount = 2;
        _traceLineRenderer.material = _lineRenderer.material;

        _curAngle = _initCurAngle;
        RecreateObstacle();
    }

    void RecreateObstacle()
    {
        foreach (GameObject node in _nodes)
            Destroy(node);
        _nodes.Clear();

        // Clear all
        _grid.SetPatch(_size * 0.5f, Vector3.zero, _size, false);

        // Add obstacle
        Vector3 obstaclePos = new Vector3(2, 0, 3);
        Vector3 obstacleSize = new Vector3(1.5f, 0, 1);
        Vector3 obstacleAngles = new Vector3(0, _curAngle, 0);

        _grid.SetPatch(obstaclePos, obstacleAngles, obstacleSize, true);

        // Draw obstacle
        Matrix4x4 m = Matrix4x4.TRS(obstaclePos, Quaternion.Euler(obstacleAngles), obstacleSize);
        Vector3[] positions =
        {
            m.MultiplyPoint(new Vector3(-0.5f, 0f, -0.5f)),
            m.MultiplyPoint(new Vector3(-0.5f, 0f, 0.5f)),
            m.MultiplyPoint(new Vector3(0.5f, 0f, 0.5f)),
            m.MultiplyPoint(new Vector3(0.5f, 0f, -0.5f)),
        };
        _obstacleLineRenderer.SetPositions(positions);

        // Draw obstacle tiles
        GameObject busyPrefab = (GameObject)Resources.Load("Busy");
        for (int x = 0; x < _grid.width; x++)
            for (int y = 0; y < _grid.height; y++)
                if (!IsWalkable(x, y))
                    _nodes.Add(Instantiate(busyPrefab, _grid.GetTilePos(x, y), Quaternion.identity));
    }

    void Update ()
    {
        Vector2 scrollDelta = Input.mouseScrollDelta;
        if (scrollDelta.sqrMagnitude > 0)
        {
            _curAngle += scrollDelta.y * 0.4f;
            _curAngle %= 360f;
            RecreateObstacle();
        }

        if (!Input.GetMouseButtonDown(0) && scrollDelta.sqrMagnitude <= 0f)
            return;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.y = 0f;
        if (mousePos.x > 0f && mousePos.x < _size.x && mousePos.z > 0f && mousePos.z < _size.z)
        {
            var ti = _grid.GetTileInfo(mousePos);
            var tilePos = _grid.GetTilePos(ti);
            if (Input.GetMouseButtonDown(0) && Input.GetMouseButton(1))
            {
                if (IsWalkable(ti.x, ti.y))
                {
                    _startPoint = tilePos;
                    if (_waypointGo == null)
                        _waypointGo = Instantiate((GameObject)Resources.Load("Waypoint"));

                    _waypointGo.transform.position = tilePos;
                }
            }

            if (_startPoint.HasValue)
            {
                _traceLineRenderer.SetPositions(new Vector3[] { _startPoint.Value, tilePos });


                // Dbg
                foreach (GameObject node in _debugNodes)
                    Destroy(node);
                _debugNodes.Clear();
                //


                var ti0 = _grid.GetTileInfo(_startPoint.Value);
                Color col = RayTraceGrid(ti0.x, ti0.y, ti.x, ti.y) ? Color.green : Color.red;
                var gr = _traceLineRenderer.colorGradient;
                gr.SetKeys(new[] { new GradientColorKey(col, 0f), new GradientColorKey(col, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0f) });
                _traceLineRenderer.colorGradient = gr;
            }
        }
    }

    //
    // Core logic
    //

    bool RayTraceGrid(int x1, int y1, int x2, int y2)
    {
        if (!TestGridValue(x1, y1) || !TestGridValue(x2, y2))
            return false;

        int dx = x2 - x1;
        int dy = y2 - y1;
        int dxAbs = (int)Mathf.Abs(dx);
        int dyAbs = (int)Mathf.Abs(dy);

        if (dxAbs + dyAbs <= 1)
            return true;

        int iterX = dx > 0f ? 1 : -1;
        int iterY = dy > 0f ? 1 : -1;

        if (dxAbs == 0 || dyAbs == 0)
        {
            bool axisX = dyAbs == 0;
            int start = axisX ? x1 : y1;
            int finish = axisX ? x2 : y2;
            int iter = axisX ? iterX : iterY;
            for (int i = start + iter; iter * i < iter * finish; i += iter)
                if (!TestGridValue(axisX ? i : x1, axisX ? y1 : i))
                    return false;

            return true;
        }

        float dVert = _grid.height;
        int y = y1;
        for (int x = 0; x <= dxAbs; x++)
        {
            int realX = x1 + x * iterX;

            // Find vertical intersection with line x3
            float x3 = realX;
            if (x < dxAbs)
                x3 += 0.5f * iterX;
            float s = (dy * (x3 - x1) + dx * y1) / (dx * dVert);
            float t = (x3 - x1) / dx;
            float yTile = (s >= 0 && s <= 1 && t >= 0 && t <= 1) ? y1 + t * dy : -1f;
            yTile += 0.5f;
            int tileYFloor = Mathf.FloorToInt(yTile);
            for (int i = y; i * iterY <= tileYFloor * iterY && i * iterY <= y2 * iterY; i += iterY)
                if (!TestGridValue(realX, i))
                    return false;

            y = tileYFloor;
        }

        return true;
    }

    bool TestGridValue(int x, int y)
    {
        //
        GameObject debugPrefab = (GameObject)Resources.Load("Debug");
        _debugNodes.Add(Instantiate(debugPrefab, _grid.GetTilePos(Mathf.FloorToInt(x), Mathf.FloorToInt(y)), Quaternion.identity));
        //

        return IsWalkable(x, y);
    }

    bool IsWalkable(int x, int y)
    {
        if (x < 0 || x >= _grid.width || y < 0 || y >= _grid.height)
            return false;

        return !_grid.GetTileInfo(x, y).value;
    }
}

static class DebugUtilities
{
    public static void Print(params object[] p)
    {
        string fmt = "Frame #: " + Time.frameCount + " ";
        for (int i = 0; i < p.Length; i++)
            fmt = fmt + "{" + i + "} ";

        Debug.Log(string.Format(fmt, p));
    }
}
