#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

public class ChronoStyleBattleSetupWindow : EditorWindow
{
    private GameObject player;
    private GameObject enemyPrefab;
    private bool createSampleEnemy = true;
    private bool cleanMissingScripts = true;
    private bool addSimplePatrol = false;

    private const string GeneratedAssetFolder = "Assets/_BattleUI_Generated";

    [MenuItem("Tools/Chrono Style Battle/Quality Auto Setup")]
    public static void Open() { GetWindow<ChronoStyleBattleSetupWindow>("Chrono Battle Setup"); }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("クロノトリガー風 ATBバトル v6", EditorStyles.boldLabel);
        EditorGUILayout.Space(8f);
        player = (GameObject)EditorGUILayout.ObjectField("Player", player, typeof(GameObject), true);
        enemyPrefab = (GameObject)EditorGUILayout.ObjectField("Enemy Prefab", enemyPrefab, typeof(GameObject), false);
        EditorGUILayout.Space(8f);
        createSampleEnemy = EditorGUILayout.Toggle("サンプル敵をMAPに置く", createSampleEnemy);
        cleanMissingScripts = EditorGUILayout.Toggle("Missing Scriptを自動削除", cleanMissingScripts);
        addSimplePatrol = EditorGUILayout.Toggle("敵に簡易パトロールを追加", addSimplePatrol);
        EditorGUILayout.Space(12f);
        if (GUILayout.Button("一括セットアップ", GUILayout.Height(36f))) RunSetup();
        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "v6 機能:\n" +
            "・足元バーをコンパクト化(キャラに被らない)\n" +
            "・対象選択カーソルが正しい位置に表示\n" +
            "・1本の競争レーン式ATBゲージ(画面上部)に変更",
            MessageType.Info);
    }

    private void RunSetup()
    {
        if (player == null) { EditorUtility.DisplayDialog("設定不足", "Playerを指定", "OK"); return; }
        if (enemyPrefab == null) { EditorUtility.DisplayDialog("設定不足", "Enemy Prefabを指定", "OK"); return; }

        Font jpFont = GetSystemJapaneseFont();
        UiAssets ui = EnsureUiAssets();

        SetupPlayer(player);
        EnsureEventSystem();

        BattleCameraController camCtl = EnsureBattleCameraController();
        Canvas canvas = EnsureCanvas();
        BattleTransitionOverlay overlay = EnsureTransitionOverlay(canvas.transform);
        RectTransform damageTextParent = EnsureDamageTextParent(canvas.transform);

        GameObject battlePanel = EnsureBattlePanelRoot(canvas.transform);
        CleanChildren(battlePanel);

        // アクションカード (画面下中央)
        GameObject actionCard = BuildActionCard(battlePanel.transform, ui);
        Text messageText = BuildText(actionCard.transform, "MessageText", "敵があらわれた！", 26,
            TextAnchor.UpperLeft, new Vector2(0.03f, 0.05f), new Vector2(0.62f, 0.95f), jpFont);
        messageText.color = new Color(0.96f, 0.97f, 1f, 1f);
        AddOutline(messageText.gameObject, new Color(0,0,0,0.85f), new Vector2(1.5f,-1.5f));

        Button attackButton = BuildButton(actionCard.transform, "AttackButton", "攻撃",
            new Vector2(0.66f, 0.55f), new Vector2(0.97f, 0.92f), ui, false, jpFont);
        Button runButton = BuildButton(actionCard.transform, "RunButton", "逃げる",
            new Vector2(0.66f, 0.08f), new Vector2(0.97f, 0.45f), ui, true, jpFont);

        // ATBレースパネル (画面上部 横長)
        AtbRacePanel atbRace = BuildAtbRacePanel(battlePanel.transform, ui, jpFont);

        // 対象選択カーソル
        BattleTargetCursor cursor = BuildTargetCursor(canvas.transform, ui);
        cursor.Setup(canvas.GetComponent<RectTransform>(), Camera.main);

        // 足元ゲージ プレハブ
        BattleActorOverhead overheadTemplate = BuildOverheadTemplate(battlePanel.transform, ui);

        // BattleManager配線
        TurnBattleManager bm = EnsureBattleManager();
        bm.battlePanel = battlePanel;
        bm.messageText = messageText;
        bm.attackButton = attackButton;
        bm.runButton = runButton;
        bm.damageTextParent = damageTextParent;
        bm.atbRace = atbRace;
        bm.targetCursor = cursor;
        bm.overheadTemplate = overheadTemplate;
        bm.cameraController = camCtl;
        bm.damageFont = jpFont;
        EditorUtility.SetDirty(bm);

        ConfigureEnemyPrefab(enemyPrefab);
        ConfigureSceneEnemies(bm, camCtl, overlay);
        if (createSampleEnemy) CreateSampleEnemyInstance(enemyPrefab, bm, camCtl, overlay);

        battlePanel.SetActive(false);

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("完了", "v6 セットアップ完了!", "OK");
    }

    private static Font GetSystemJapaneseFont()
    {
        string[] candidates = { "Yu Gothic UI", "Yu Gothic", "YuGothic", "Meiryo UI", "Meiryo",
            "Hiragino Sans", "Noto Sans CJK JP", "Noto Sans JP", "MS Gothic", "MS UI Gothic" };
        foreach (var n in candidates)
        {
            try { Font f = Font.CreateDynamicFontFromOSFont(n, 32);
                  if (f != null && f.fontNames != null && f.fontNames.Length > 0) return f;
            } catch { }
        }
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private struct UiAssets
    {
        public Sprite cardBg, actionBg, buttonAttack, buttonRun;
        public Sprite hpBg, hpFill, accent, cursor;
        public Sprite atbTrackBg, runnerCircle, runnerGlow;
    }

    private UiAssets EnsureUiAssets()
    {
        if (!AssetDatabase.IsValidFolder(GeneratedAssetFolder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(GeneratedAssetFolder).Replace('\\','/'), Path.GetFileName(GeneratedAssetFolder));
        return new UiAssets
        {
            cardBg       = MakeRoundRect("CardBg",       384, 96,  new Color(0.06f,0.09f,0.16f,0.88f), new Color(0.45f,0.85f,1f,0.85f), 10, 2),
            actionBg     = MakeRoundRect("ActionBg",     768, 144, new Color(0.06f,0.09f,0.16f,0.88f), new Color(0.45f,0.85f,1f,0.85f), 12, 2),
            buttonAttack = MakeRoundRect("BtnAttack",    256, 80,  new Color(0.18f,0.45f,0.85f,0.96f), new Color(0.70f,0.95f,1f,1f), 10, 2),
            buttonRun    = MakeRoundRect("BtnRun",       256, 80,  new Color(0.85f,0.30f,0.30f,0.96f), new Color(1f,0.85f,0.75f,1f), 10, 2),
            hpBg         = MakeRoundRect("HpBg",         256, 14,  new Color(0.02f,0.04f,0.08f,0.95f), new Color(0.30f,0.55f,0.85f,0.70f), 5, 1),
            hpFill       = MakeSolid    ("HpFill",       256, 14,  Color.white),
            accent       = MakeSolid    ("Accent",       4,   1,   Color.white),
            cursor       = MakeTriangle ("Cursor",       48,  48),
            atbTrackBg   = MakeRoundRect("AtbTrackBg",   1024, 38, new Color(0.04f,0.06f,0.12f,0.85f), new Color(0.45f,0.85f,1f,0.85f), 10, 2),
            runnerCircle = MakeCircle   ("RunnerCircle", 64,  64,  new Color(1,1,1,1), 3),
            runnerGlow   = MakeGlow     ("RunnerGlow",   96,  96),
        };
    }

    private Sprite MakeRoundRect(string name, int w, int h, Color innerColor, Color borderColor, int corner, int border)
    {
        string path = $"{GeneratedAssetFolder}/{name}.png";
        Sprite ex = AssetDatabase.LoadAssetAtPath<Sprite>(path); if (ex != null) return ex;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            int dxL=x, dxR=w-1-x, dyB=y, dyT=h-1-y;
            Color c = innerColor;
            bool corn = (dxL<corner&&dyB<corner)||(dxR<corner&&dyB<corner)||(dxL<corner&&dyT<corner)||(dxR<corner&&dyT<corner);
            if (corn)
            {
                int cx = (dxL<corner) ? corner : w-1-corner;
                int cy = (dyB<corner) ? corner : h-1-corner;
                float dist = Mathf.Sqrt((x-cx)*(x-cx)+(y-cy)*(y-cy));
                if (dist > corner) c = new Color(0,0,0,0);
                else if (dist > corner-border) c = borderColor;
            }
            else { if (dxL<border||dxR<border||dyB<border||dyT<border) c = borderColor; }
            pixels[y*w+x] = c;
        }
        tex.SetPixels(pixels); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG()); AssetDatabase.ImportAsset(path);
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null) {
            ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true;
            TextureImporterSettings s = new TextureImporterSettings();
            ti.ReadTextureSettings(s); s.spriteBorder = new Vector4(corner,corner,corner,corner);
            ti.SetTextureSettings(s); ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private Sprite MakeSolid(string name, int w, int h, Color c)
    {
        string path = $"{GeneratedAssetFolder}/{name}.png";
        Sprite ex = AssetDatabase.LoadAssetAtPath<Sprite>(path); if (ex != null) return ex;
        Texture2D tex = new Texture2D(w,h,TextureFormat.RGBA32,false);
        Color[] px = new Color[w*h]; for(int i=0;i<px.Length;i++) px[i]=c;
        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG()); AssetDatabase.ImportAsset(path);
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null) { ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private Sprite MakeTriangle(string name, int w, int h)
    {
        string path = $"{GeneratedAssetFolder}/{name}.png";
        Sprite ex = AssetDatabase.LoadAssetAtPath<Sprite>(path); if (ex != null) return ex;
        Texture2D tex = new Texture2D(w,h,TextureFormat.RGBA32,false);
        Color[] px = new Color[w*h];
        Color tri = new Color(1f,0.9f,0.3f,1f);
        Color border = new Color(0.2f,0.1f,0,1f);
        for (int y=0;y<h;y++)
        {
            float t = (float)y/(h-1);
            int halfW = Mathf.RoundToInt((w*0.5f)*t);
            int cx = w/2;
            for (int x=0;x<w;x++)
            {
                int dx = x-cx; Color c;
                if (Mathf.Abs(dx) <= halfW) {
                    if (Mathf.Abs(dx)>=halfW-2 || y<=1 || y>=h-2) c=border; else c=tri;
                } else c=new Color(0,0,0,0);
                px[y*w+x]=c;
            }
        }
        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG()); AssetDatabase.ImportAsset(path);
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null) { ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private Sprite MakeCircle(string name, int w, int h, Color baseColor, int border)
    {
        string path = $"{GeneratedAssetFolder}/{name}.png";
        Sprite ex = AssetDatabase.LoadAssetAtPath<Sprite>(path); if (ex != null) return ex;
        Texture2D tex = new Texture2D(w,h,TextureFormat.RGBA32,false);
        Color[] px = new Color[w*h];
        Vector2 c = new Vector2(w*0.5f, h*0.5f);
        float r = Mathf.Min(w,h)*0.5f - 0.5f;
        for (int y=0;y<h;y++) for (int x=0;x<w;x++)
        {
            float d = Vector2.Distance(new Vector2(x+0.5f,y+0.5f), c);
            Color col;
            if (d > r) col = new Color(0,0,0,0);
            else if (d > r - border) col = new Color(1,1,1,1);
            else col = baseColor;
            px[y*w+x] = col;
        }
        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG()); AssetDatabase.ImportAsset(path);
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null) { ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private Sprite MakeGlow(string name, int w, int h)
    {
        string path = $"{GeneratedAssetFolder}/{name}.png";
        Sprite ex = AssetDatabase.LoadAssetAtPath<Sprite>(path); if (ex != null) return ex;
        Texture2D tex = new Texture2D(w,h,TextureFormat.RGBA32,false);
        Color[] px = new Color[w*h];
        Vector2 c = new Vector2(w*0.5f, h*0.5f);
        float maxR = Mathf.Min(w,h)*0.5f;
        for (int y=0;y<h;y++) for (int x=0;x<w;x++)
        {
            float d = Vector2.Distance(new Vector2(x+0.5f,y+0.5f), c) / maxR;
            float a = Mathf.Exp(-d*d*3f) * 0.85f;
            px[y*w+x] = new Color(1f, 0.95f, 0.4f, Mathf.Clamp01(a));
        }
        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG()); AssetDatabase.ImportAsset(path);
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null) { ti.textureType = TextureImporterType.Sprite; ti.alphaIsTransparency = true; ti.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private void SetupPlayer(GameObject playerObj)
    {
        if (playerObj.GetComponent<PS5PlayerController>() == null) playerObj.AddComponent<PS5PlayerController>();
        Rigidbody2D rb = playerObj.GetComponent<Rigidbody2D>(); if (rb == null) rb = playerObj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f; rb.freezeRotation = true; rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        if (playerObj.GetComponent<Collider2D>() == null) { var c = playerObj.AddComponent<CircleCollider2D>(); c.radius = 0.25f; }
        if (playerObj.GetComponent<BattleActor>() == null) playerObj.AddComponent<PartyMember>();
        if (playerObj.GetComponent<CharacterAnimatorDriver>() == null) playerObj.AddComponent<CharacterAnimatorDriver>();
        EditorUtility.SetDirty(playerObj);
    }

    private void EnsureEventSystem()
    {
        EventSystem es = FindSceneObject<EventSystem>();
        if (es == null) es = new GameObject("EventSystem").AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        if (es.GetComponent<InputSystemUIInputModule>() == null) es.gameObject.AddComponent<InputSystemUIInputModule>();
#else
        if (es.GetComponent<StandaloneInputModule>() == null) es.gameObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private BattleCameraController EnsureBattleCameraController()
    {
        Camera cam = Camera.main; if (cam == null) cam = FindSceneObject<Camera>();
        if (cam == null) { GameObject co = new GameObject("Main Camera"); co.tag = "MainCamera"; cam = co.AddComponent<Camera>(); cam.orthographic = true; cam.transform.position = new Vector3(0,0,-10); }
        BattleCameraController c = cam.GetComponent<BattleCameraController>();
        if (c == null) c = cam.gameObject.AddComponent<BattleCameraController>();
        c.targetCamera = cam; c.battleZoomSize = 3f; c.uiBottomFraction = 0.15f; c.CaptureOriginal();
        EditorUtility.SetDirty(c);
        return c;
    }

    private Canvas EnsureCanvas()
    {
        GameObject co = FindSceneGameObject("BattleCanvas");
        if (co == null) co = new GameObject("BattleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = co.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
        CanvasScaler s = co.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920,1080); s.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private BattleTransitionOverlay EnsureTransitionOverlay(Transform parent)
    {
        GameObject obj = FindChild(parent, "BattleTransitionOverlay");
        if (obj == null)
        {
            obj = new GameObject("BattleTransitionOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(BattleTransitionOverlay));
            obj.transform.SetParent(parent, false);
        }
        StretchFull(obj.GetComponent<RectTransform>());
        Image img = obj.GetComponent<Image>(); img.color = new Color(0,0,0,0); img.raycastTarget = false;
        obj.transform.SetAsLastSibling();
        return obj.GetComponent<BattleTransitionOverlay>();
    }

    private RectTransform EnsureDamageTextParent(Transform parent)
    {
        GameObject obj = FindChild(parent, "DamageTextParent");
        if (obj == null) { obj = new GameObject("DamageTextParent", typeof(RectTransform)); obj.transform.SetParent(parent, false); }
        StretchFull(obj.GetComponent<RectTransform>()); obj.transform.SetAsLastSibling();
        return obj.GetComponent<RectTransform>();
    }

    private GameObject EnsureBattlePanelRoot(Transform parent)
    {
        GameObject panel = FindChild(parent, "BattlePanel");
        if (panel == null) { panel = new GameObject("BattlePanel", typeof(RectTransform)); panel.transform.SetParent(parent, false); }
        Image existingImg = panel.GetComponent<Image>(); if (existingImg != null) DestroyImmediate(existingImg);
        StretchFull(panel.GetComponent<RectTransform>());
        return panel;
    }

    private void CleanChildren(GameObject obj)
    {
        for (int i = obj.transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(obj.transform.GetChild(i).gameObject);
    }

    private GameObject BuildActionCard(Transform parent, UiAssets ui)
    {
        GameObject co = new GameObject("ActionCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        co.transform.SetParent(parent, false);
        RectTransform rt = co.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.06f, 0.04f); rt.anchorMax = new Vector2(0.94f, 0.22f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        Image bg = co.GetComponent<Image>();
        bg.sprite = ui.actionBg; bg.type = Image.Type.Sliced; bg.color = Color.white;
        return co;
    }

    private Button BuildButton(Transform parent, string name, string label, Vector2 amin, Vector2 amax, UiAssets ui, bool isRun, Font jpFont)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = amin; rt.anchorMax = amax; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        Image img = obj.GetComponent<Image>(); img.sprite = isRun ? ui.buttonRun : ui.buttonAttack;
        img.type = Image.Type.Sliced; img.color = Color.white;
        Button b = obj.GetComponent<Button>();
        ColorBlock cb = b.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(1.15f,1.15f,1.15f,1f);
        cb.pressedColor = new Color(0.7f,0.85f,1f,1f); cb.disabledColor = new Color(0.55f,0.55f,0.55f,0.6f);
        b.colors = cb;
        Text t = BuildText(obj.transform, "Text", label, 26, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, jpFont);
        t.color = Color.white; t.fontStyle = FontStyle.Bold;
        AddOutline(t.gameObject, new Color(0,0,0,0.9f), new Vector2(1.5f,-1.5f));
        return b;
    }

    // ============================================================
    // ATB レーストラック (画面上部・横長)
    // ============================================================
    private AtbRacePanel BuildAtbRacePanel(Transform parent, UiAssets ui, Font jpFont)
    {
        GameObject panel = new GameObject("AtbRacePanel", typeof(RectTransform), typeof(AtbRacePanel));
        panel.transform.SetParent(parent, false);
        RectTransform rt = panel.GetComponent<RectTransform>();
        // 画面上部 中央寄り (左右にマージンを確保)
        rt.anchorMin = new Vector2(0.10f, 0.86f);
        rt.anchorMax = new Vector2(0.90f, 0.95f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        // トラック背景
        GameObject track = new GameObject("Track", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        track.transform.SetParent(panel.transform, false);
        RectTransform trt = track.GetComponent<RectTransform>();
        StretchFull(trt);
        Image trackBg = track.GetComponent<Image>();
        trackBg.sprite = ui.atbTrackBg; trackBg.type = Image.Type.Sliced; trackBg.color = Color.white;
        trackBg.raycastTarget = false;

        // 「READY」マーカー (右端)
        GameObject readyMark = new GameObject("ReadyMark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        readyMark.transform.SetParent(track.transform, false);
        RectTransform rmRt = readyMark.GetComponent<RectTransform>();
        rmRt.anchorMin = new Vector2(1f, 0f); rmRt.anchorMax = new Vector2(1f, 1f);
        rmRt.pivot = new Vector2(1f, 0.5f);
        rmRt.sizeDelta = new Vector2(8f, 0f);
        rmRt.anchoredPosition = new Vector2(-4f, 0f);
        Image rmImg = readyMark.GetComponent<Image>();
        rmImg.sprite = ui.accent; rmImg.color = new Color(1f,0.95f,0.4f,1f); rmImg.raycastTarget = false;

        // ラベル "ATB"
        Text labelAtb = BuildText(track.transform, "AtbLabel", "ATB", 16, TextAnchor.MiddleLeft,
            new Vector2(0f, 0f), new Vector2(0f, 1f), jpFont);
        RectTransform labelRt = labelAtb.GetComponent<RectTransform>();
        labelRt.pivot = new Vector2(1f, 0.5f);
        labelRt.sizeDelta = new Vector2(40f, 0f);
        labelRt.anchoredPosition = new Vector2(-8f, 0f);
        labelAtb.color = new Color(0.9f, 0.95f, 1f, 0.7f);
        labelAtb.fontStyle = FontStyle.Bold;

        // ランナーコンテナ (track の中、padding分内側)
        GameObject runnerContainer = new GameObject("RunnerContainer", typeof(RectTransform));
        runnerContainer.transform.SetParent(track.transform, false);
        RectTransform rcRt = runnerContainer.GetComponent<RectTransform>();
        rcRt.anchorMin = Vector2.zero; rcRt.anchorMax = Vector2.one;
        // 左右パディング (アイコン半径分)
        rcRt.offsetMin = new Vector2(20f, 0f);
        rcRt.offsetMax = new Vector2(-20f, 0f);

        // ランナーテンプレート
        AtbRunner runnerTemplate = BuildRunnerTemplate(panel.transform, ui, jpFont);

        AtbRacePanel atb = panel.GetComponent<AtbRacePanel>();
        atb.track = rcRt;
        atb.runnerContainer = rcRt;
        atb.runnerTemplate = runnerTemplate;
        return atb;
    }

    private AtbRunner BuildRunnerTemplate(Transform parent, UiAssets ui, Font jpFont)
    {
        GameObject runner = new GameObject("AtbRunnerTemplate", typeof(RectTransform), typeof(AtbRunner));
        runner.transform.SetParent(parent, false);
        RectTransform rt = runner.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(36f, 36f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        // グロー (満タン時の発光)
        GameObject glow = new GameObject("Glow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        glow.transform.SetParent(runner.transform, false);
        RectTransform glowRt = glow.GetComponent<RectTransform>();
        glowRt.anchorMin = Vector2.zero; glowRt.anchorMax = Vector2.one;
        glowRt.offsetMin = new Vector2(-12,-12); glowRt.offsetMax = new Vector2(12,12);
        Image glowImg = glow.GetComponent<Image>();
        glowImg.sprite = ui.runnerGlow; glowImg.color = Color.white; glowImg.raycastTarget = false;
        glow.SetActive(false);

        // BG (色付き円・透明の中身)
        GameObject bgGo = new GameObject("Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(runner.transform, false);
        RectTransform bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        Image bgImg = bgGo.GetComponent<Image>();
        bgImg.sprite = ui.runnerCircle; bgImg.color = new Color(0.45f,0.85f,1f,1f); bgImg.raycastTarget = false;

        // Ring (外周色) — bgImgで兼用するので簡略化
        // ラベル
        Text label = BuildText(runner.transform, "Label", "??", 16, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, jpFont);
        label.color = Color.white; label.fontStyle = FontStyle.Bold;
        AddOutline(label.gameObject, new Color(0,0,0,0.9f), new Vector2(1.5f,-1.5f));

        AtbRunner ar = runner.GetComponent<AtbRunner>();
        ar.iconBg = bgImg;
        ar.iconRing = bgImg; // 同じ
        ar.label = label;
        ar.readyGlow = glow;

        runner.SetActive(false);
        return ar;
    }

    // ============================================================
    // 足元ゲージ (コンパクト、キャラ下に表示)
    // ============================================================
    private BattleActorOverhead BuildOverheadTemplate(Transform parent, UiAssets ui)
    {
        GameObject root = new GameObject("OverheadTemplate", typeof(RectTransform), typeof(Canvas), typeof(BattleActorOverhead));
        root.transform.SetParent(parent, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 80;

        RectTransform rt = root.GetComponent<RectTransform>();
        // 80 x 24 px → world scale 0.01 で 0.8 x 0.24 ユニット → 横0.8 縦0.24
        rt.sizeDelta = new Vector2(80f, 24f);
        root.transform.localScale = Vector3.one * 0.01f;

        // HPバー (上段)
        GameObject hp = BuildOverheadGauge(root.transform, "HpBar",
            anchorY: 0.5f + 0.27f, height: 8f, ui: ui,
            fillColor: new Color(0.30f,0.85f,0.55f,1f));

        // MPバー (下段、敵にはなし)
        GameObject mp = BuildOverheadGauge(root.transform, "MpBar",
            anchorY: 0.5f - 0.27f, height: 6f, ui: ui,
            fillColor: new Color(0.30f,0.65f,1f,1f));

        BattleActorOverhead bo = root.GetComponent<BattleActorOverhead>();
        bo.hpSlider = hp.GetComponent<Slider>();
        bo.hpFill = hp.transform.Find("FillArea/Fill").GetComponent<Image>();
        bo.mpSlider = mp.GetComponent<Slider>();
        bo.mpFill = mp.transform.Find("FillArea/Fill").GetComponent<Image>();
        // 配置を足元のかなり下に
        bo.worldOffset = new Vector3(0f, -0.75f, 0f);

        root.SetActive(false);
        return bo;
    }

    private GameObject BuildOverheadGauge(Transform parent, string name, float anchorY, float height, UiAssets ui, Color fillColor)
    {
        GameObject so = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
        so.transform.SetParent(parent, false);
        RectTransform sRt = so.GetComponent<RectTransform>();
        sRt.anchorMin = new Vector2(0f, anchorY - 0.1f);
        sRt.anchorMax = new Vector2(1f, anchorY + 0.1f);
        sRt.offsetMin = Vector2.zero; sRt.offsetMax = Vector2.zero;
        sRt.sizeDelta = new Vector2(0f, height);

        Image bg = so.GetComponent<Image>();
        bg.sprite = ui.hpBg; bg.type = Image.Type.Sliced; bg.color = Color.white; bg.raycastTarget = false;

        GameObject fa = new GameObject("FillArea", typeof(RectTransform));
        fa.transform.SetParent(so.transform, false);
        RectTransform faRt = fa.GetComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero; faRt.anchorMax = Vector2.one;
        faRt.offsetMin = new Vector2(1, 1); faRt.offsetMax = new Vector2(-1, -1);

        GameObject fo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fo.transform.SetParent(fa.transform, false);
        RectTransform fRt = fo.GetComponent<RectTransform>();
        fRt.anchorMin = Vector2.zero; fRt.anchorMax = Vector2.one;
        fRt.offsetMin = Vector2.zero; fRt.offsetMax = Vector2.zero;
        Image fillImg = fo.GetComponent<Image>(); fillImg.sprite = ui.hpFill; fillImg.color = fillColor; fillImg.raycastTarget = false;

        Slider slider = so.GetComponent<Slider>();
        slider.fillRect = fRt; slider.handleRect = null; slider.targetGraphic = bg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f; slider.interactable = false;

        return so;
    }

    private BattleTargetCursor BuildTargetCursor(Transform parent, UiAssets ui)
    {
        GameObject co = new GameObject("TargetCursor", typeof(RectTransform), typeof(BattleTargetCursor));
        co.transform.SetParent(parent, false);
        StretchFull(co.GetComponent<RectTransform>());
        co.transform.SetAsLastSibling();

        GameObject icon = new GameObject("CursorIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        icon.transform.SetParent(co.transform, false);
        RectTransform irt = icon.GetComponent<RectTransform>();
        irt.sizeDelta = new Vector2(48f, 48f);
        irt.anchorMin = new Vector2(0f, 0f); irt.anchorMax = new Vector2(0f, 0f);
        irt.pivot = new Vector2(0.5f, 0f);
        Image img = icon.GetComponent<Image>(); img.sprite = ui.cursor; img.color = Color.white; img.raycastTarget = false;
        icon.SetActive(false);

        BattleTargetCursor cur = co.GetComponent<BattleTargetCursor>();
        cur.cursorRect = irt; cur.cursorImage = img;
        return cur;
    }

    private Text BuildText(Transform parent, string name, string content, int fontSize, TextAnchor align, Vector2 amin, Vector2 amax, Font jpFont)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = amin; rt.anchorMax = amax; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        Text t = obj.GetComponent<Text>();
        t.text = content; t.fontSize = fontSize; t.alignment = align;
        t.color = Color.white; t.font = jpFont != null ? jpFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.supportRichText = true;
        return t;
    }

    private static void AddOutline(GameObject obj, Color color, Vector2 distance)
    {
        Outline o = obj.GetComponent<Outline>(); if (o == null) o = obj.AddComponent<Outline>();
        o.effectColor = color; o.effectDistance = distance;
    }

    private TurnBattleManager EnsureBattleManager()
    {
        GameObject obj = FindSceneGameObject("BattleManager");
        if (obj == null) obj = new GameObject("BattleManager");
        TurnBattleManager bm = obj.GetComponent<TurnBattleManager>();
        if (bm == null) bm = obj.AddComponent<TurnBattleManager>();
        return bm;
    }

    private void ConfigureEnemyPrefab(GameObject prefabAsset)
    {
        string path = AssetDatabase.GetAssetPath(prefabAsset);
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")) return;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (cleanMissingScripts) RemoveMissingScriptsRecursive(root);
        if (root.GetComponent<EnemyStatus>() == null) root.AddComponent<EnemyStatus>();
        Collider2D col = root.GetComponent<Collider2D>();
        if (col == null) { CircleCollider2D c = root.AddComponent<CircleCollider2D>(); c.radius = 0.35f; col = c; }
        col.isTrigger = true;
        if (root.GetComponent<MapEnemyEncounter>() == null) root.AddComponent<MapEnemyEncounter>();
        if (addSimplePatrol && root.GetComponent<MapEnemyPatrol2D>() == null) root.AddComponent<MapEnemyPatrol2D>();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private void ConfigureSceneEnemies(TurnBattleManager bm, BattleCameraController cc, BattleTransitionOverlay ov)
    {
        MapEnemyEncounter[] enemies = Resources.FindObjectsOfTypeAll<MapEnemyEncounter>();
        foreach (var e in enemies)
        {
            if (EditorUtility.IsPersistent(e)) continue;
            e.battleManager = bm; e.cameraController = cc; e.transitionOverlay = ov;
            Collider2D c = e.GetComponent<Collider2D>(); if (c != null) c.isTrigger = true;
            EditorUtility.SetDirty(e);
        }
    }

    private void CreateSampleEnemyInstance(GameObject prefabAsset, TurnBattleManager bm, BattleCameraController cc, BattleTransitionOverlay ov)
    {
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
        if (inst == null) inst = Instantiate(prefabAsset);
        Undo.RegisterCreatedObjectUndo(inst, "Create sample enemy");
        inst.name = prefabAsset.name + "_MapEnemy";
        inst.transform.position = player.transform.position + new Vector3(2.5f, 0f, 0f);
        if (inst.GetComponent<EnemyStatus>() == null) inst.AddComponent<EnemyStatus>();
        Collider2D col = inst.GetComponent<Collider2D>();
        if (col == null) { var cc2 = inst.AddComponent<CircleCollider2D>(); cc2.radius = 0.35f; col = cc2; }
        col.isTrigger = true;
        MapEnemyEncounter mee = inst.GetComponent<MapEnemyEncounter>();
        if (mee == null) mee = inst.AddComponent<MapEnemyEncounter>();
        if (addSimplePatrol && inst.GetComponent<MapEnemyPatrol2D>() == null) inst.AddComponent<MapEnemyPatrol2D>();
        mee.battleManager = bm; mee.cameraController = cc; mee.transitionOverlay = ov;
        Selection.activeGameObject = inst; EditorUtility.SetDirty(inst);
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
    private static GameObject FindChild(Transform parent, string name)
    {
        foreach (Transform c in parent.GetComponentsInChildren<Transform>(true))
            if (c.name == name) return c.gameObject;
        return null;
    }
    private static GameObject FindSceneGameObject(string name)
    {
        GameObject[] objs = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var o in objs)
        {
            if (o.name != name) continue;
            if (EditorUtility.IsPersistent(o)) continue;
            if (!o.scene.IsValid()) continue;
            return o;
        }
        return null;
    }
    private static T FindSceneObject<T>() where T : Object
    {
        T[] objs = Resources.FindObjectsOfTypeAll<T>();
        foreach (var o in objs)
        {
            if (EditorUtility.IsPersistent(o)) continue;
            Component c = o as Component;
            if (c != null && c.gameObject.scene.IsValid()) return o;
            GameObject go = o as GameObject;
            if (go != null && go.scene.IsValid()) return o;
        }
        return null;
    }
    private static int RemoveMissingScriptsRecursive(GameObject obj)
    {
        int r = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
        foreach (Transform c in obj.transform) r += RemoveMissingScriptsRecursive(c.gameObject);
        return r;
    }
}
#endif
