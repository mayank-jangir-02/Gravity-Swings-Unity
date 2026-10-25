using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

[System.Serializable]
public class HazardData { public float[] tris; public float[] segs; }

// Line-by-line port of the web version. All numbers are in "level pixels" (y pointing DOWN, like the web page);
// they are converted to Unity world units only when drawing.
public class OvoGame : MonoBehaviour
{
    [Tooltip("Level prefabs, in play order")] public LevelSettings[] levels;
    [Tooltip("Drag OvoSprite.shader here")] public Shader shader;
    public Camera cam;

    const float RAD = 16f;
    class Fx { public float x, y, t, s; }

    Material matPlain, matInv;
    Sprite circleSprite;
    LevelSettings lv; int levelIndex; SpriteRenderer art;
    int W, H; byte[] wd; float ppu = 100f; Vector2 origin;
    float[] tris = new float[0], segs = new float[0];
    Vector2[] cps; Vector2 gate; int swapsLeft; bool hasHazardZones, hasNoSwapZones;

    float px, py, vx, vy; int p = 1, cp;
    float dead, won, cool, time, camX, camY;
    bool clicked, contact;
    readonly List<Fx> fx = new List<Fx>();
    Vector2 N = new Vector2(0, -1);

    GameObject ballRoot; SpriteRenderer bodyR, tipR, winR; LineRenderer antenna;
    readonly List<LineRenderer> lines = new List<LineRenderer>(); int used;
    readonly List<SpriteRenderer> dots = new List<SpriteRenderer>();

    // ---------------------------------------------------------------- setup
    void Start()
    {
        if (!cam) cam = Camera.main;
        if (!shader) shader = Shader.Find("Ovo/Sprite");
        if (!cam || !shader || levels == null || levels.Length == 0 || !levels[0])
        { Debug.LogError("Ovo: fill Levels, Shader and Cam on the Game object (or run menu Ovo -> Set up Level 1 (picture))."); enabled = false; return; }
        if (Keyboard.current == null) Debug.LogWarning("Ovo: no keyboard detected. Set Project Settings -> Player -> Active Input Handling to 'Input System Package (New)' or 'Both'.");
        cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
        matPlain = MakeMat(false); matInv = MakeMat(true);
        circleSprite = MakeCircle();
        ballRoot = new GameObject("Ball");
        bodyR = Spr("Body", ballRoot.transform, 30012); tipR = Spr("Tip", ballRoot.transform, 30012);
        antenna = NewLine(ballRoot.transform, false);
        winR = Spr("Win", null, 30020); winR.enabled = false;
        LoadLevel(0);
    }

    Material MakeMat(bool invert)
    {
        var m = new Material(shader);
        m.SetFloat("_Invert", invert ? 1f : 0f);
        m.SetFloat("_Src", (float)(invert ? BlendMode.OneMinusDstColor : BlendMode.One));
        m.SetFloat("_Dst", (float)BlendMode.OneMinusSrcAlpha);
        return m;
    }

    static Sprite MakeCircle()
    {
        const int n = 128;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var c = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - n / 2f + .5f) * (x - n / 2f + .5f) + (y - n / 2f + .5f) * (y - n / 2f + .5f));
                c[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(n / 2f - d) * 255f));
            }
        t.SetPixels32(c); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(.5f, .5f), n);   // 1 world unit across
    }

    SpriteRenderer Spr(string n, Transform parent, int order)
    {
        var g = new GameObject(n); g.transform.SetParent(parent, false);
        var r = g.AddComponent<SpriteRenderer>();
        r.sprite = circleSprite; r.sharedMaterial = matInv; r.sortingOrder = order;
        return r;
    }

    LineRenderer NewLine(Transform parent, bool world)
    {
        var g = new GameObject("Line"); g.transform.SetParent(parent, false);
        var l = g.AddComponent<LineRenderer>();
        l.sharedMaterial = matInv; l.useWorldSpace = world; l.sortingOrder = 30011;
        return l;
    }

    void LoadLevel(int i)
    {
        if (lv) { lv.gameObject.SetActive(false); Destroy(lv.gameObject); }
        levelIndex = i;
        lv = Instantiate(levels[i]);
        if (!lv.art || !lv.art.sprite || lv.checkpoints == null || lv.checkpoints.Length == 0 || !lv.gate)
        { Debug.LogError("Ovo: the Level prefab is missing Art, Checkpoints or Gate. Run menu Ovo -> Set up Level 1 (picture)."); enabled = false; return; }
        if (!lv.art.sprite.texture.isReadable)
        { Debug.LogError("Ovo: level.png needs Read/Write enabled. Run menu Ovo -> Set up Level 1 (picture)."); enabled = false; return; }
        art = lv.art; art.sharedMaterial = matPlain; art.sortingOrder = 0;
        var tex = art.sprite.texture; W = tex.width; H = tex.height; ppu = art.sprite.pixelsPerUnit;
        var b = art.bounds; origin = new Vector2(b.min.x, b.max.y);
        var c = tex.GetPixels32(); wd = new byte[W * H];
        for (int y = 0; y < H; y++) { int s = (H - 1 - y) * W, d = y * W; for (int x = 0; x < W; x++) wd[d + x] = (byte)(c[s + x].r > 127 ? 1 : 0); }
        tris = new float[0]; segs = new float[0];
        if (lv.hazardsJson) { var h = JsonUtility.FromJson<HazardData>(lv.hazardsJson.text); if (h.tris != null) tris = h.tris; if (h.segs != null) segs = h.segs; }
        cps = new Vector2[lv.checkpoints.Length];
        for (int k = 0; k < cps.Length; k++) cps[k] = ToPx(lv.checkpoints[k].position);
        gate = ToPx(lv.gate.position);
        swapsLeft = lv.maxSwaps;
        hasHazardZones = lv.GetComponentInChildren<HazardZone>() != null;
        hasNoSwapZones = lv.GetComponentInChildren<NoSwapZone>() != null;
        Physics2D.SyncTransforms();
        cp = 0; won = 0; dead = 0; fx.Clear(); Spawn();
        camX = px - 300; camY = py - 400;
    }

    void NextLevel() { LoadLevel((levelIndex + 1) % levels.Length); }

    Vector2 ToPx(Vector3 w) { return new Vector2((w.x - origin.x) * ppu, (origin.y - w.y) * ppu); }
    Vector3 W3(float x, float y) { return new Vector3(origin.x + x / ppu, origin.y - y / ppu, 0f); }

    void Spawn() { px = cps[cp].x; py = cps[cp].y; vx = vy = 0; p = 1; }

    // ---------------------------------------------------------------- world queries
    int Raw(float x, float y)
    {
        int xi = (int)x, yi = (int)y;
        return xi < 0 || xi >= W || yi < 0 || yi >= H ? 0 : wd[yi * W + xi];
    }

    bool ZoneAt<T>(float x, float y) where T : Component
    {
        foreach (var c in Physics2D.OverlapPointAll(W3(x, y))) if (c.GetComponent<T>() != null) return true;
        return false;
    }

    bool Hit(float x, float y)
    {
        for (int i = 0; i < tris.Length; i += 6)
        {
            float x1 = tris[i], y1 = tris[i + 1], x2 = tris[i + 2], y2 = tris[i + 3], x3 = tris[i + 4], y3 = tris[i + 5];
            if (x < Mathf.Min(x1, Mathf.Min(x2, x3)) || x > Mathf.Max(x1, Mathf.Max(x2, x3))) continue;
            float d1 = (x - x2) * (y1 - y2) - (x1 - x2) * (y - y2);
            float d2 = (x - x3) * (y2 - y3) - (x2 - x3) * (y - y3);
            float d3 = (x - x1) * (y3 - y1) - (x3 - x1) * (y - y1);
            if (!((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0))) return true;
        }
        for (int i = 0; i < segs.Length; i += 4)
        {
            if (Mathf.Abs(x - segs[i]) > 16f) continue;
            float dx = segs[i + 2] - segs[i], dy = segs[i + 3] - segs[i + 1];
            float u = Mathf.Clamp01(((x - segs[i]) * dx + (y - segs[i + 1]) * dy) / (dx * dx + dy * dy));
            float ex = x - segs[i] - u * dx, ey = y - segs[i + 1] - u * dy;
            if (Mathf.Sqrt(ex * ex + ey * ey) < 7f) return true;
        }
        return hasHazardZones && ZoneAt<HazardZone>(x, y);
    }

    // ---------------------------------------------------------------- input
    void Update()
    {
        HandleInput();
        float dt = Mathf.Min(.033f, Time.deltaTime);
        Step(dt);
        Draw();
    }

    void HandleInput()
    {
        var kb = Keyboard.current; if (kb == null) return;
        if (kb.ctrlKey.isPressed || kb.altKey.isPressed || kb.leftMetaKey.isPressed || kb.rightMetaKey.isPressed) return;
        foreach (var k in kb.allKeys)          // ANY key except the move keys swaps. The mouse does nothing.
        {
            if (!k.wasPressedThisFrame) continue;
            var kc = k.keyCode;
            if (kc == Key.A || kc == Key.D || kc == Key.LeftArrow || kc == Key.RightArrow || kc == Key.Escape || kc == Key.Tab || (kc >= Key.F1 && kc <= Key.F12)) continue;
            Flip(); break;
        }
    }

    void Flip()
    {
        if (won > 2f) { NextLevel(); return; }
        if (dead > 0 || won > 0 || cool > 0) return;
        if (lv.maxSwaps > 0 && swapsLeft <= 0) return;
        if (hasNoSwapZones && ZoneAt<NoSwapZone>(px, py)) return;
        p ^= 1; cool = .12f; clicked = true;
        if (lv.maxSwaps > 0) swapsLeft--;
        fx.Add(new Fx { x = px, y = py, t = 0, s = 240 });
        if (Raw(px, py) != p)
        {
            float best = 1e9f, bx = 0, by = 0;
            for (int a = 0; a < 24; a++)
            {
                float ca = Mathf.Cos(a * .2618f), sa = Mathf.Sin(a * .2618f);
                for (float d = 4; d < 500; d += 4)
                    if (Raw(px + ca * d, py + sa * d) == p) { if (d < best) { best = d; bx = ca; by = sa; } break; }
            }
            if (best < 500) { vx = vx * .3f + bx * 700f; vy = vy * .3f + by * 700f; }
        }
    }

    // ---------------------------------------------------------------- physics (same as the web version)
    bool Collide()
    {
        bool c = false;
        for (int it = 0; it < 8; it++)
        {
            float nx = 0, ny = 0; int k = 0;
            for (int a = 0; a < 16; a++)
            {
                float ax = px + Mathf.Cos(a * .3927f) * RAD, ay = py + Mathf.Sin(a * .3927f) * RAD;
                if (Raw(ax, ay) != p) { nx += px - ax; ny += py - ay; k++; }
            }
            if (k == 0) break;
            c = true;
            float l = Mathf.Sqrt(nx * nx + ny * ny); if (l == 0) l = 1;
            N = new Vector2(nx / l, ny / l); px += N.x * 1.5f; py += N.y * 1.5f;
        }
        if (c)
        {
            float vn = vx * N.x + vy * N.y;
            if (vn < 0) { float e = -vn > 170f ? .5f : 0f; vx -= (1 + e) * vn * N.x; vy -= (1 + e) * vn * N.y; }
        }
        return c;
    }

    void Step(float dt)
    {
        time += dt; cool -= dt;
        foreach (var f in fx) f.t += dt * 1.6f;
        fx.RemoveAll(f => f.t >= 1f);
        if (won > 0) { won += dt; return; }
        if (dead > 0) { dead -= dt; if (dead <= 0) Spawn(); return; }
        var kb = Keyboard.current;
        float ax = 0;
        if (kb != null)
        {
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) ax += 1;
            if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) ax -= 1;
        }
        vy += (p == 1 ? 1 : -1) * 1700f * dt; vx += ax * 1400f * dt;
        float sp = Mathf.Sqrt(vx * vx + vy * vy);
        if (sp > 850f) { vx *= 850f / sp; vy *= 850f / sp; }
        contact = false;
        int n = Mathf.Max(1, Mathf.CeilToInt(sp * dt / 5f));
        for (int i = 0; i < n; i++)
        {
            px += vx * dt / n; py += vy * dt / n;
            if (px < RAD) { px = RAD; vx = 0; }
            if (px > W - RAD) { px = W - RAD; vx = 0; }
            if (py < RAD) { py = RAD; vy = 0; }
            if (py > H - RAD) { py = H - RAD; vy = 0; }
            if (Raw(px, py) == p && Collide()) contact = true;
        }
        float fr = Mathf.Pow(contact ? .75f : .9f, dt);
        vx *= fr; if (!contact) vy *= fr;
        for (int i = 0; i < 5; i++)
        {
            float a = i * 1.2566f, r = i > 0 ? RAD * .6f : 0f;
            if (Hit(px + Mathf.Cos(a) * r, py + Mathf.Sin(a) * r)) { dead = .7f; fx.Add(new Fx { x = px, y = py, t = 0, s = 180 }); return; }
        }
        for (int i = 1; i < cps.Length; i++)
            if (i > cp && Vector2.Distance(new Vector2(px, py), cps[i]) < lv.checkpointRadius) { cp = i; fx.Add(new Fx { x = cps[i].x, y = cps[i].y, t = 0, s = 120 }); }
        if (Vector2.Distance(new Vector2(px, py), gate) < lv.gateRadius) won = .001f;
    }

    // ---------------------------------------------------------------- drawing
    LineRenderer Line(float alpha, float widthPx, int caps)
    {
        if (used == lines.Count) lines.Add(NewLine(transform, true));
        var l = lines[used++];
        l.enabled = true; l.loop = false; l.numCapVertices = caps; l.widthMultiplier = widthPx / ppu;
        var c = new Color(1, 1, 1, alpha); l.startColor = c; l.endColor = c;
        return l;
    }

    void Ring(float cx, float cy, float r, float alpha, float widthPx)
    {
        var l = Line(alpha, widthPx, 0); const int n = 48;
        l.loop = true; l.positionCount = n;
        for (int i = 0; i < n; i++) { float a = i * Mathf.PI * 2f / n; l.SetPosition(i, W3(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r)); }
    }

    void Draw()
    {
        used = 0;
        float vw = 800f * cam.aspect;
        float tx = Mathf.Max(0, Mathf.Min(W - vw, px - vw / 2 + vx * .25f));
        float ty = Mathf.Max(0, Mathf.Min(H - 800f, py - 400f + vy * .1f));
        float f60 = Time.deltaTime * 60f;
        camX += (tx - camX) * (1 - Mathf.Pow(1 - .12f, f60));
        camY += (ty - camY) * (1 - Mathf.Pow(1 - .09f, f60));
        Vector3 cpos = W3(camX + vw / 2, camY + 400f); cpos.z = -10f;
        cam.transform.position = cpos; cam.orthographicSize = 400f / ppu;

        for (int i = 1; i < cps.Length; i++)
            for (int k = 0; k < 3; k++) Ring(cps[i].x, cps[i].y, 8 + k * 9 + Mathf.Sin(time * 2 + k) * 2, cp >= i ? 1f : .45f, 2f);
        for (int k = 0; k < 4; k++) Ring(gate.x, gate.y, 10 + k * 14 + Mathf.Sin(time * 3 - k) * 4, 1f, 3f);
        foreach (var f in fx) Ring(f.x, f.y, f.t * f.s, 1 - f.t, 3f);

        ballRoot.SetActive(dead <= 0);
        if (dead <= 0)
        {
            float sp = Mathf.Sqrt(vx * vx + vy * vy);
            float sh = contact ? Mathf.Min(2.2f, sp / 160f) : 0f, sq = Mathf.Min(.25f, sp / 3400f);
            int dir = p == 1 ? 1 : -1;
            ballRoot.transform.position = W3(px + (Random.value - .5f) * sh, py + (Random.value - .5f) * sh);
            ballRoot.transform.localScale = new Vector3(1 - sq, 1 + sq, 1);
            bodyR.transform.localScale = Vector3.one * (2 * RAD / ppu);
            Vector2 P0 = new Vector2(0, dir * RAD * .8f), C = new Vector2(vx * .01f, dir * RAD * 1.5f), P2 = new Vector2(vx * .02f + 3f, dir * RAD * 1.75f);
            antenna.widthMultiplier = 2.5f / ppu; antenna.numCapVertices = 4; antenna.positionCount = 8;
            for (int i = 0; i < 8; i++)
            {
                float t = i / 7f, u = 1 - t;
                Vector2 q = u * u * P0 + 2 * u * t * C + t * t * P2;
                antenna.SetPosition(i, new Vector3(q.x / ppu, q.y / ppu, 0));
            }
            tipR.transform.localPosition = new Vector3((vx * .02f + 3f) / ppu, dir * RAD * 1.8f / ppu, 0);
            tipR.transform.localScale = Vector3.one * (6f / ppu);
        }

        if (!clicked && won <= 0)   // first-time hint: pulsing keyboard-key glyph, no text
        {
            float cy = py - 70f, w = 44f, h = 16f, r = 4f;
            var o = Line(1f, 2.5f, 0); o.loop = true; o.positionCount = 24;
            float[] sx = { 1, -1, -1, 1 }, sy = { 1, 1, -1, -1 };
            int idx = 0;
            for (int q = 0; q < 4; q++)
                for (int k = 0; k < 6; k++)
                {
                    float a = (q * 90f + k * 18f) * Mathf.Deg2Rad;
                    o.SetPosition(idx++, W3(px + sx[q] * (w / 2 - r) + Mathf.Cos(a) * r, cy + sy[q] * (h / 2 - r) + Mathf.Sin(a) * r));
                }
            var fl = Line(.4f + .6f * Mathf.Abs(Mathf.Sin(time * 4)), h - 8f, 0); fl.positionCount = 2;
            fl.SetPosition(0, W3(px - (w - 8f) / 2, cy)); fl.SetPosition(1, W3(px + (w - 8f) / 2, cy));
        }

        while (dots.Count < lv.maxSwaps) dots.Add(Spr("Dot", transform, 30012));
        for (int i = 0; i < dots.Count; i++)
        {
            bool on = lv.maxSwaps > 0 && i < swapsLeft && dead <= 0 && won <= 0;
            dots[i].enabled = on;
            if (on) { dots[i].transform.position = W3(px + (i - (swapsLeft - 1) / 2f) * 14f, py - 40f); dots[i].transform.localScale = Vector3.one * (8f / ppu); }
        }

        winR.enabled = won > 0;
        if (won > 0)
        {
            float t = Mathf.Min(1f, won / 2.5f);
            winR.transform.position = W3(px, py);
            winR.transform.localScale = Vector3.one * (2f * t * Mathf.Sqrt(vw * vw + 640000f) / ppu);
            winR.color = new Color(1, 1, 1, t * .9f);
        }
        for (int i = used; i < lines.Count; i++) lines[i].enabled = false;
    }
}
