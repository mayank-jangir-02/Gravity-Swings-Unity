#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu:  Ovo -> Set up Level 1 (picture)
// Does ALL the manual setup for you: texture import settings, Level1 prefab, checkpoints, gate, Game object.
public static class OvoImageSetup
{
    [System.Serializable] class JInfo { public float[] cps; public float[] gate; }
    const string Dir = "Assets/Level1";

    static void Say(string msg) { EditorUtility.DisplayDialog("Ovo", msg, "OK"); }

    [MenuItem("Ovo/Set up Level 1 (picture)")]
    static void Setup()
    {
        string png = Dir + "/level.png", json = Dir + "/level.json";
        if (!File.Exists(png) || !File.Exists(json)) { Say("Put level.png and level.json inside the folder Assets/Level1 first."); return; }
        var shader = Shader.Find("Ovo/Sprite");
        if (!shader) { Say("Shader 'Ovo/Sprite' not found. Import OvoSprite.shader and wait for Unity to finish compiling (select the shader and read its Inspector for errors)."); return; }

        // 1. texture import settings (what you would otherwise tick by hand)
        var ti = AssetImporter.GetAtPath(png) as TextureImporter;
        if (ti == null) { Say("level.png was not imported as a texture. Re-import it."); return; }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f;
        ti.mipmapEnabled = false;
        ti.isReadable = true;
        ti.alphaIsTransparency = false;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Bilinear;
        ti.maxTextureSize = 8192;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        var ts = new TextureImporterSettings();
        ti.ReadTextureSettings(ts);
        ts.spriteAlignment = (int)SpriteAlignment.TopLeft;
        ts.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(ts);
        ti.SaveAndReimport();
        AssetDatabase.Refresh();

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
        if (!tex || !sprite) { Say("Could not load level.png as a sprite."); return; }
        if (tex.width != 4600 || tex.height != 3000) { Say("level.png is " + tex.width + "x" + tex.height + " but must be 4600x3000. Download it again from the exporter page."); return; }

        // 2. Level1 prefab
        var info = JsonUtility.FromJson<JInfo>(File.ReadAllText(json));
        if (info == null || info.cps == null || info.cps.Length < 2 || info.gate == null || info.gate.Length < 2) { Say("level.json is not the right file. Download it again from the exporter page."); return; }
        var root = new GameObject("Level1");
        var ls = root.AddComponent<LevelSettings>();
        var art = new GameObject("Art"); art.transform.SetParent(root.transform, false);
        var sr = art.AddComponent<SpriteRenderer>(); sr.sprite = sprite;
        ls.art = sr;
        ls.hazardsJson = AssetDatabase.LoadAssetAtPath<TextAsset>(json);
        var list = new List<Transform>();
        for (int i = 0; i < info.cps.Length / 2; i++)
        {
            var g = new GameObject(i == 0 ? "Start" : "Checkpoint" + i); g.transform.SetParent(root.transform, false);
            g.transform.position = new Vector3(info.cps[2 * i] / 100f, -info.cps[2 * i + 1] / 100f, 0f);
            list.Add(g.transform);
        }
        ls.checkpoints = list.ToArray();
        var gt = new GameObject("Gate"); gt.transform.SetParent(root.transform, false);
        gt.transform.position = new Vector3(info.gate[0] / 100f, -info.gate[1] / 100f, 0f);
        ls.gate = gt.transform;
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Dir + "/Level1.prefab");
        Object.DestroyImmediate(root);
        if (!prefab) { Say("Could not save the Level1 prefab."); return; }

        // 3. Game object in the open scene
        var game = Object.FindAnyObjectByType<OvoGame>();
        if (!game) game = new GameObject("Game").AddComponent<OvoGame>();
        game.levels = new[] { prefab.GetComponent<LevelSettings>() };
        game.shader = shader;
        game.cam = Camera.main;
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Say("Done. Press the Play button, then click the Game view once.");
    }
}
#endif
