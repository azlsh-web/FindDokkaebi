using System.IO;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// MvpSceneBuilder 의 UI 만들기 도우미 부분이에요. 씬 조립 순서는 MvpSceneBuilder.cs 의 Build() 에 있어요.
public static partial class MvpSceneBuilder
{
    // ───────────── UI 만들기 도우미들 ─────────────
    // 좌표 규칙 : 모든 UI 는 화면 "위쪽 가운데"를 기준점으로,
    //             pos.x = 가운데에서 오른쪽(+)/왼쪽(-), pos.y = 위에서 아래로 떨어진 거리

    static Transform NewEmpty(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch(go.GetComponent<RectTransform>());
        return go.transform;
    }

    // 푸른 도깨비불 세 개 (GameManager 가 둘레를 돌려요). size = 불 크기 (화면 끝에서도 다 보이게 작게)
    static RectTransform[] DokkaebiFires(Transform orbit, float size)
    {
        var fires = new RectTransform[3];
        for (int i = 0; i < fires.Length; i++)
        {
            var glow = NewImage("Fire_" + i, orbit, new Color32(150, 220, 255, 150));    // 불 뒤의 푸른 빛
            glow.sprite = T("Common_SoftPuff");
            glow.raycastTarget = false;
            Anchor(glow.rectTransform, 0.5f, 0.5f, Vector2.zero, new Vector2(size, size));
            var fire = NewImage("Flame", glow.transform, Color.white);
            fire.sprite = T("Magnifier_DokkaebiFire");
            fire.preserveAspect = true;
            fire.raycastTarget = false;
            Anchor(fire.rectTransform, 0.5f, 0.5f, new Vector2(0, size * 0.1f), new Vector2(size, size * 4f / 3f));
            fires[i] = glow.rectTransform;
        }
        return fires;
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(parent, false);
        img.color = color;
        return img;
    }

    // 그림(Sprite) 한 장 놓기 (비율 유지, 클릭은 통과)
    static Image NewSprite(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size)
    {
        var img = NewImage(name, parent, Color.white);
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        Place(img.rectTransform, pos, size);
        return img;
    }

    static TMP_Text NewText(string name, Transform parent, string text, float size, Vector2 pos, Vector2 box,
                            TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false)
    {
        var t = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        t.transform.SetParent(parent, false);
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = TextColor;
        t.alignment = align;
        // Jua 는 원래 굵은 글꼴이라 따로 굵게 하지 않아요 (Noto 일 때만 제목·버튼을 굵게)
        if (bold && !font.name.StartsWith("Jua")) t.fontStyle = FontStyles.Bold;
        t.raycastTarget = false; // 글자가 클릭을 가로채지 않게
        Place(t.rectTransform, pos, box);
        return t;
    }

    // 글자 버튼 (알약 모양). primary = 코랄(이 화면의 주된 행동) / false = 크림(보조 행동). 테두리는 그림에 들어 있어요
    static Button NewButton(string name, Transform parent, string label, Vector2 pos, Vector2 box, bool primary = true)
    {
        var img = NewImage(name, parent, Color.white);
        img.sprite = primary ? pillCoral : pillCream;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = img.sprite.rect.height / box.y;   // 알약 양 끝 동그라미가 버튼 높이에 딱 맞게
        Place(img.rectTransform, pos, box);
        var button = img.gameObject.AddComponent<Button>();

        var t = NewText("Label", img.transform, label, box.y >= 120 ? 46 : 38, Vector2.zero, Vector2.zero, bold: true);
        Stretch(t.rectTransform);
        return button;
    }

    // 크림(또는 흰) 카드 : 진갈색 굵은 테두리가 있는 둥근 네모
    // 쪽지 질감 그림 한 장 (늘리지 않고 그대로 깔아요. 크기는 그림 비율에 맞춰 놓아요). 카드처럼 클릭은 막아요
    static Image NewPaper(string name, Transform parent, string sprite)
    {
        var img = NewImage(name, parent, Color.white);
        img.sprite = T(sprite);
        return img;
    }

    static Transform NewCard(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var img = NewImage(name, parent, Color.white);
        img.sprite = cardSprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f / K;                           // 모서리 둥글기를 시안과 같은 크기로 (카드 그림은 시안 크기)
        Place(img.rectTransform, pos, size);
        return img.transform;
    }

    // 가로로 버튼을 늘어놓는 줄. 꺼진 버튼은 빼고 남은 버튼을 가운데로 모아요.
    static Transform NewRow(string name, Transform parent, float y, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), new Vector2(0, y), new Vector2(1060, height));
        var layout = go.GetComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 22;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        return go.transform;
    }

    // 부모 안의 기준점(ax, ay : 0~1)에 붙여서 놓기. pos 는 기준점에서 떨어진 거리
    static void Anchor(RectTransform rt, float ax, float ay, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(ax, ay);
        rt.pivot = new Vector2(ax, ay);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    // 동그란 도구 버튼 : [묶음] 안에 [Frame(선택 테두리)] + [Edge(진갈색 테두리)] + [Button(동그라미) + Icon]
    static Button NewRoundTool(string name, Transform row, Sprite icon, Color color, float size, out GameObject frame)
    {
        var holder = new GameObject(name, typeof(RectTransform));
        holder.transform.SetParent(row, false);
        holder.GetComponent<RectTransform>().sizeDelta = new Vector2(size, size);

        var frameImg = NewImage("Frame", holder.transform, FrameColor);
        frameImg.sprite = circleSprite;
        Stretch(frameImg.rectTransform, -12); // 버튼보다 크게 → 고른 도구 둘레에 주황 고리
        frameImg.raycastTarget = false;
        frame = frameImg.gameObject;

        var edge = NewImage("Edge", holder.transform, OutlineColor);
        edge.sprite = circleSprite;
        Stretch(edge.rectTransform);
        edge.raycastTarget = false;

        var img = NewImage("Button", holder.transform, color);
        img.sprite = circleSprite;
        Stretch(img.rectTransform, 5);       // 진갈색 테두리 5 만큼 안쪽
        var button = img.gameObject.AddComponent<Button>();

        if (icon != null)
        {
            var iconImg = NewImage("Icon", img.transform, Color.white);
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            Stretch(iconImg.rectTransform, size * 0.14f);
        }
        return button;
    }

    // ── 팀 시안 좌표 도우미 ──
    // 시안 그림(2248x3952)의 픽셀 좌표 → 화면(1080 폭) 좌표. K = 1080 / 2248
    const float K = 1080f / 2248f;
    const float FrameHeight = 3952f * K;      // ≈ 1899

    static Sprite T(string name) => LoadSprite("UI/" + name + ".png");

    // 남은 시간 초 게이지의 위 끝 (화면 1080 폭 기준). 위 줄 아래 ~ 전체 화면 쪽지(FullArea 위 300 + 여백) 사이
    //   막대 가운데 줄. 불꽃 꼭대기가 위 줄(돋보기 아래 끝 176)에 닿지 않게 규격표 칸(200 ~ 298)보다 조금 내렸어요
    const float GaugeMid = 272f;
    const float GaugeWidth = 980f;   // 화면 1080 - 좌우 여백 50 x 2

    // 시안의 상자 (x0,y0)-(x1,y1) 자리에 놓기 (부모 = 화면 크기 패널)
    static void PlaceM(RectTransform rt, float x0, float y0, float x1, float y1)
    {
        Place(rt, new Vector2((x0 + x1) * 0.5f * K - 540f, y0 * K), new Vector2((x1 - x0) * K, (y1 - y0) * K));
    }

    // 시안 상자 parentBox 안의 상자 자리에 놓기 (부모 = parentBox 자리에 놓인 그림)
    static void PlaceIn(RectTransform rt, float[] parentBox, float x0, float y0, float x1, float y1)
    {
        float pcx = (parentBox[0] + parentBox[2]) * 0.5f;
        Place(rt, new Vector2(((x0 + x1) * 0.5f - pcx) * K, (y0 - parentBox[1]) * K), new Vector2((x1 - x0) * K, (y1 - y0) * K));
    }

    // 시안에서 오린 그림 한 장을 원래 자리에 (클릭은 통과)
    static Image TeamImage(string name, Transform parent, Sprite sprite, float x0, float y0, float x1, float y1)
    {
        var img = NewImage(name, parent, Color.white);
        img.sprite = sprite;
        img.raycastTarget = false;
        PlaceM(img.rectTransform, x0, y0, x1, y1);
        return img;
    }

    // 시안 그림 버튼 : [묶음] 안에 [Frame(고른 표시 코랄 고리)] + [Button(시안 그림)]
    static Button TeamTool(string name, Transform parent, Sprite sprite, float x0, float y0, float x1, float y1, float ringPad, out GameObject frame)
    {
        var holder = new GameObject(name, typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        PlaceM(holder.GetComponent<RectTransform>(), x0, y0, x1, y1);
        var ring = NewImage("Frame", holder.transform, Color.white);
        ring.sprite = T("Common_SelectRing");
        ring.raycastTarget = false;
        Stretch(ring.rectTransform, -ringPad * K);
        frame = ring.gameObject;
        var img = NewImage("Button", holder.transform, Color.white);
        img.sprite = sprite;
        img.preserveAspect = true;
        Stretch(img.rectTransform);
        return img.gameObject.AddComponent<Button>();
    }

    // 세로로 긴 내 그림을 정사각형 카드 가운데에 비율 그대로 놓기
    static void FitDrawing(RectTransform rt, float cardSize, float padding)
    {
        float height = cardSize - padding * 2f;
        float width = height * DrawingBoard.TextureWidth / DrawingBoard.TextureHeight;
        Anchor(rt, 0.5f, 0.5f, Vector2.zero, new Vector2(width, height));
    }

    // 화면 전체 패널을 dy 만큼 아래로 (안의 것들이 같이 내려가요)
    static void ShiftDown(Transform panel, float dy)
    {
        ((RectTransform)panel).offsetMax = new Vector2(0f, -dy);
    }

    // 위쪽 가운데 기준으로 위치·크기 정하기
    static void Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(pos.x, -pos.y);
        rt.sizeDelta = size;
    }

    // 왼쪽 위(right=false) 또는 오른쪽 위(right=true) 모서리에 붙이기. top = 위에서, side = 옆에서 떨어진 거리
    static void PlaceCorner(RectTransform rt, bool right, float top, Vector2 size, float side = 40f)
    {
        float x = right ? 1f : 0f;
        rt.anchorMin = rt.anchorMax = new Vector2(x, 1f);
        rt.pivot = new Vector2(x, 1f);
        rt.anchoredPosition = new Vector2(right ? -side : side, -top);
        rt.sizeDelta = size;
    }

    // 부모 크기에 꽉 채우기 (inset 만큼 안쪽으로. 음수면 바깥으로)
    // 긴 폰에서 Frame 이 늘어나도 자리가 맞게 : 위쪽 기준으로 놓은 것들을 구역에 따라 다시 붙여요 (보이는 자리는 기준 화면에서 그대로)
    //   아래 끝이 TopZone 위 → 맨 위에 / 위 끝이 BottomZone 아래 → 맨 아래에 / 나머지 → 가운데에
    //   Frame 높이만큼 꽉 찬 묶음(패널 등) 안으로 들어가며 바꿔요. 크기가 정해진 부모 안의 것은 부모를 따라가요.
    const float TopZone = 320f, BottomZone = 1450f;
    // 한 장면으로 모여 있어야 하는 화면 : 아래쪽 것도 가운데에 붙여요 (챕터 카드 묶음, 소풍 그림의 돗자리와 사탕 통)
    static readonly string[] KeepTogether = { "ChapterPanel", "EndingPanel", "RoomEndPanel" };
    // 구역과 다르게 붙일 것 (경로 → 0 아래, 0.5 가운데, 1 위)
    static readonly Dictionary<string, float> PinOverride = new Dictionary<string, float>
    {
        { "EndingPanel/DialogueBox", 1f },     // 소풍 장면 대사는 하늘 쪽 맨 위에
        { "RoomEndPanel/DialogueBox", 1f },    // 방 끝 장면 대사도 맨 위에
    };
    static void PinToScreenEdges(RectTransform parent, string path = "", bool together = false)
    {
        foreach (Transform child in parent)
        {
            var rt = child as RectTransform;
            if (rt == null) continue;
            string childPath = path + child.name;
            bool childTogether = together || System.Array.IndexOf(KeepTogether, childPath) >= 0;
            bool stretchedY = rt.anchorMin.y == 0f && rt.anchorMax.y == 1f;
            if (stretchedY)
            {
                if (Mathf.Abs(rt.offsetMin.y) < 0.01f && Mathf.Abs(rt.offsetMax.y) < 0.01f) PinToScreenEdges(rt, childPath + "/", childTogether);
                continue;
            }
            if (rt.anchorMin.y != 1f || rt.anchorMax.y != 1f) continue;      // 이미 가운데나 아래 기준
            float pivotFromTop = -rt.anchoredPosition.y;
            float h = rt.rect.height;
            float top = pivotFromTop - (1f - rt.pivot.y) * h, bottom = top + h;
            float anchorY = bottom <= TopZone ? 1f : top >= BottomZone && !together ? 0f : 0.5f;
            if (PinOverride.TryGetValue(childPath, out float forced)) anchorY = forced;
            if (anchorY == 1f) continue;
            rt.anchorMin = new Vector2(rt.anchorMin.x, anchorY);
            rt.anchorMax = new Vector2(rt.anchorMax.x, anchorY);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, FrameHeight * (1f - anchorY) - pivotFromTop);
        }
    }

    static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    // ── 연출 개편 도우미 : 화면을 360x633 으로 본 좌표 (x, y, 폭, 높이) → 실제는 3배 ──
    static void M(RectTransform rt, float x, float y, float w, float h)
    {
        Place(rt, new Vector2((x + w * 0.5f) * 3f - 540f, y * 3f), new Vector2(w * 3f, h * 3f));
    }

    // 부모 안의 왼쪽 위 기준 좌표 (같은 3배 단위)
    static void PIn(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x * 3f, -y * 3f);
        rt.sizeDelta = new Vector2(w * 3f, h * 3f);
    }

    static Image MImage(string name, Transform parent, Sprite sprite, float x, float y, float w, float h)
    {
        var img = NewImage(name, parent, Color.white);
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        M(img.rectTransform, x, y, w, h);
        return img;
    }

    // 발밑을 기준으로 움직이게 (자리는 그대로)
    static void PivotBottom(RectTransform rt)
    {
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition += new Vector2(0f, -rt.sizeDelta.y);
    }

    // 생각 말풍선으로 이어지는 작은 동그라미 (테두리 + 속)
    static void BubbleDot(Transform parent, float x, float y, float size)
    {
        var edge = MImage("BubbleDot", parent, circleSprite, x, y, size, size);
        edge.color = OutlineColor;
        var fill = NewImage("Fill", edge.transform, new Color32(255, 253, 246, 255));
        fill.sprite = circleSprite;
        fill.raycastTarget = false;
        Stretch(fill.rectTransform, 10);
    }

    static SimpleAnim Anim(GameObject go, SimpleAnim.Kind kind, float time, float delay, float distance, float angle = 0f)
    {
        var a = go.AddComponent<SimpleAnim>();
        a.kind = kind;
        a.popTime = time;
        a.delay = delay;
        a.distance = distance;
        a.angle = angle;
        return a;
    }

    // 화면 패널 : 켜질 때 서서히 나타나요
    static Transform NewPanel(string name, Transform parent)
    {
        var panel = NewEmpty(name, parent);
        panel.gameObject.AddComponent<CanvasGroup>();
        Anim(panel.gameObject, SimpleAnim.Kind.FadeIn, 0.35f, 0f, 0f);
        return panel;
    }

    // 화면 전체를 덮는 투명한 터치 영역
    static Button NewTap(string name, Transform parent)
    {
        var img = NewImage(name, parent, new Color(1f, 1f, 1f, 0f));
        Stretch(img.rectTransform);
        return img.gameObject.AddComponent<Button>();
    }

    // 대사 상자 : 모든 대사가 같은 크기 · 같은 자리예요
    static DialogueBox NewDialogueBox(Transform parent, Sprite face)
    {
        var card = NewPaper("DialogueBox", parent, "Dialogue_Box").transform;   // 아트팀 그림 (2098 x 649), 늘리지 않아요
        M((RectTransform)card, 12, 513, 336, 104);
        var faceImg = NewImage("Face", card, Color.white);
        faceImg.sprite = face;
        faceImg.preserveAspect = true;
        faceImg.raycastTarget = false;
        Anchor(faceImg.rectTransform, 0f, 0.5f, new Vector2(39, 0), new Vector2(186, 168));
        var line = NewText("Line", card, "", 48, Vector2.zero, Vector2.zero, TextAlignmentOptions.Left);
        line.font = talkFont;
        line.fontStyle = FontStyles.Normal;
        line.lineSpacing = 6;
        line.rectTransform.anchorMin = Vector2.zero;
        line.rectTransform.anchorMax = Vector2.one;
        line.rectTransform.offsetMin = new Vector2(255, 24);
        line.rectTransform.offsetMax = new Vector2(-90, -24);
        var arrow = NewImage("Arrow", card, Color.white);
        arrow.sprite = T("Common_NextArrow");
        arrow.raycastTarget = false;
        Anchor(arrow.rectTransform, 1f, 0f, new Vector2(-42, 36), new Vector2(54, 54));
        var box = card.gameObject.AddComponent<DialogueBox>();
        var so = new SerializedObject(box);
        Set(so, "face", faceImg);
        Set(so, "text", line);
        Set(so, "arrow", arrow);
        so.ApplyModifiedPropertiesWithoutUndo();
        return box;
    }

    // SerializedObject 칸 채우기. 칸 이름이 틀리면 바로 알려줘요.
    static void Set(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null) throw new System.Exception($"{so.targetObject.GetType().Name} 에 '{field}' 칸이 없어요");
        p.objectReferenceValue = value;
    }

    // 끝 장면 사탕 통 (11/23 다시 넣음) : 유리 뒤쪽 → 사탕 15개(한 줄 3개, 아래부터) → 테두리 · 뚜껑. 통 그림은 make_candy_jar.py
    //   x, y, w, h 는 M 단위 (화면 폭 360). 사탕은 통이 나온 뒤 하나씩 톡톡 나타나요 (CandyJar.Fill 이 개수만큼 켜요)
    static CandyJar NewCandyJar(Transform parent, Sprite candy, float x, float y, float w, float h, float delay)
    {
        var go = new GameObject("CandyJar", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        M((RectTransform)go.transform, x, y, w, h);
        Anim(go, SimpleAnim.Kind.Pop, 0.45f, delay, 0f);
        var back = NewImage("Back", go.transform, Color.white);
        back.sprite = T("Ending_CandyJarBack");
        back.raycastTarget = false;
        Stretch(back.rectTransform);
        float sx = w / 62f, sy = h / 80f;               // 옛 소풍 통(62 x 80) 기준 자리를 크기에 맞춰요
        var candies = new Object[15];
        for (int k = 0; k < candies.Length; k++)
        {
            int row = k / 3, col = k % 3;
            var c = NewImage($"Candy_{k + 1}", go.transform, Color.white);
            c.sprite = candy;
            c.preserveAspect = true;
            c.raycastTarget = false;
            PIn(c.rectTransform, (6 + col * 16 + (row % 2) * 2) * sx, (61 - row * 9) * sy, 17 * sx, 12 * sy);
            Anim(c.gameObject, SimpleAnim.Kind.Pop, 0.3f, delay + 0.5f + k * 0.08f, 0f);
            candies[k] = c.gameObject;
        }
        var front = NewImage("Front", go.transform, Color.white);
        front.sprite = T("Ending_CandyJarFront");
        front.raycastTarget = false;
        Stretch(front.rectTransform);
        var jar = go.AddComponent<CandyJar>();
        var so = new SerializedObject(jar);
        SetArray(so, "candies", candies);
        so.ApplyModifiedPropertiesWithoutUndo();
        return jar;
    }

    static void SetArray(SerializedObject so, string field, Object[] items)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p == null) throw new System.Exception($"{so.targetObject.GetType().Name} 에 '{field}' 칸이 없어요");
        p.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}
