using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds the game's HUD at runtime so the scene file doesn't need hand-editing:
/// settings bar (dash threshold, letter gap), press gauge with a threshold hairline,
/// a Morse chart that narrows as you type, and a first-run tutorial.
/// The existing TelegraphSystem texts are re-parented into this layout.
/// </summary>
public class MorseHud : MonoBehaviour
{
	// Palette: quiet dark background, one accent.
	static readonly Color Bg = Hex("#121513");
	static readonly Color Surface = Hex("#1A1F1C");
	static readonly Color Line = Hex("#2C342F");
	static readonly Color TextCol = Hex("#D9E0DB");
	static readonly Color Dim = Hex("#6B766F");
	static readonly Color Accent = Hex("#7CFFB2");

	const string PrefOnboarded = "morse.onboarded";
	const string PrefDashMs = "morse.dashMs";
	const string PrefGapMs = "morse.gapMs";
	const string PrefChart = "morse.chartOpen";

	TelegraphSystem telegraph;
	Sprite rounded;
	RectTransform root;
	RectTransform stage; // centre area; shrinks when the chart is open
	Canvas canvas;
	GraphicRaycaster raycaster;

	// Layout (landscape / portrait)
	CanvasScaler scaler;
	RectTransform settingsBar, titleRt, helpRt, chartButtonRt;
	GridLayoutGroup chartGrid;
	TextMeshProUGUI hint;
	bool? portraitApplied;
	Vector2Int lastScreen;

	// Web / touch
	RectTransform startOverlay;
	MorseTapKey tapKey;

	// Settings bar
	Slider dashSlider, gapSlider;
	TextMeshProUGUI dashLabel, gapLabel;
	int shownDashMs = -1, shownGapMs = -1;
	bool dashChanged, gapChanged;

	// Gauge
	RectTransform gaugeFill, gaugeLine;
	Image gaugeFillImg;
	TextMeshProUGUI gaugeLabel;
	float demoPress = -1f; // >= 0 while a demo press drives the gauge

	// Chart
	RectTransform chartPanel;
	Image chartButtonBg;
	TextMeshProUGUI chartButtonLabel;
	readonly List<ChartCell> chartCells = new List<ChartCell>();
	string chartFilter = null;

	// Tutorial
	RectTransform captionRow;
	TextMeshProUGUI caption;
	Coroutine tutorial;
	Coroutine demo;
	bool sawDot, sawDash, sawLetter;

	class ChartCell
	{
		public string code;
		public TextMeshProUGUI letter;
		public TextMeshProUGUI morse;
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	static void Boot()
	{
		var telegraph = FindObjectOfType<TelegraphSystem>();
		if (telegraph == null || FindObjectOfType<MorseHud>() != null) return;
		new GameObject("MorseHud").AddComponent<MorseHud>().Init(telegraph);
	}

	public void Init(TelegraphSystem t)
	{
		telegraph = t;
		Time.timeScale = 1f;
		rounded = MakeRoundedSprite(32, 8);

		BuildCanvas();
		AdoptLegacyTexts();
		BuildSettingsBar();
		BuildGauge();
		BuildChart();
		BuildCaption();
		BuildHint();
		BuildTapKey();
		BuildStartOverlay();

		telegraph.IsOverControl = IsOverControl;
		telegraph.SymbolEntered += s => { if (s == '.') sawDot = true; else sawDash = true; };
		telegraph.LetterCommitted += _ => sawLetter = true;

		ApplyLayout(true);

		// On the web, a first press is needed for focus and audio. Without the start screen,
		// a first-time player goes straight into the tutorial.
		if (startOverlay == null && PlayerPrefs.GetInt(PrefOnboarded, 0) == 0)
		{
			StartTutorial();
		}
	}

	// ---------------------------------------------------------------- build

	void BuildCanvas()
	{
		if (EventSystem.current == null)
		{
			new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
		}

		var cam = Camera.main;
		if (cam != null)
		{
			cam.clearFlags = CameraClearFlags.SolidColor;
			cam.backgroundColor = Bg;
		}

		var go = new GameObject("HUD Canvas", typeof(RectTransform));
		go.transform.SetParent(transform, false);
		canvas = go.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 10;
		scaler = go.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1280, 720);
		scaler.matchWidthOrHeight = 0.5f;
		raycaster = go.AddComponent<GraphicRaycaster>();

		root = (RectTransform)go.transform;
		var bg = Panel("Background", root, Bg);
		bg.sprite = null;
		Stretch(bg.rectTransform);

		stage = Rect("Stage", root);
		Stretch(stage);
	}

	void AdoptLegacyTexts()
	{
		var legacyCanvas = telegraph.collectionText.canvas != null
			? telegraph.collectionText.canvas.rootCanvas
			: null;

		Adopt(telegraph.feedbackText, 120, 50, 28, Dim);
		Adopt(telegraph.collectionText, 40, 110, 64, TextCol);
		Adopt(telegraph.liveMorseCodeText, -50, 60, 44, Accent);
		telegraph.collectionText.enableAutoSizing = true;
		telegraph.collectionText.fontSizeMin = 28;
		telegraph.collectionText.fontSizeMax = 64;
		telegraph.liveMorseCodeText.characterSpacing = 30;

		// The old canvas only held these texts, the label and the speed slider.
		telegraph.thresholdSlider = null;
		if (legacyCanvas != null) legacyCanvas.gameObject.SetActive(false);
	}

	void Adopt(TextMeshProUGUI text, float y, float height, float fontSize, Color color)
	{
		var rt = text.rectTransform;
		rt.SetParent(stage, false);
		Place(rt, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(-80, height));
		rt.localScale = Vector3.one;
		rt.localRotation = Quaternion.identity;
		text.fontSize = fontSize;
		text.color = color;
		text.alignment = TextAlignmentOptions.Center;
		text.enableWordWrapping = true;
		text.raycastTarget = false;
	}

	void BuildSettingsBar()
	{
		var bar = settingsBar = Rect("Settings", root);
		Place(bar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(-48, 44));

		var title = Label("Title", bar, "MORSE TYPER", 18, Dim, TextAlignmentOptions.Left);
		titleRt = title.rectTransform;
		title.characterSpacing = 12;
		Place(title.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(260, 0));

		int dashMs = PlayerPrefs.GetInt(PrefDashMs, Mathf.RoundToInt(telegraph.longPressThreshold * 1000f));
		int gapMs = PlayerPrefs.GetInt(PrefGapMs, Mathf.RoundToInt(telegraph.LetterGap * 1000f));
		telegraph.longPressThreshold = dashMs / 1000f;
		telegraph.LetterGap = gapMs / 1000f;

		// Right-aligned group, laid out right to left.
		float x = 0;
		var help = MakeButton(bar, "?", 44, ref x);
		help.onClick.AddListener(StartTutorial);
		helpRt = (RectTransform)help.transform;
		var chart = MakeButton(bar, "Chart", 88, ref x);
		chartButtonBg = chart.GetComponent<Image>();
		chartButtonRt = (RectTransform)chart.transform;
		chartButtonLabel = chart.GetComponentInChildren<TextMeshProUGUI>();
		chart.onClick.AddListener(() => SetChartOpen(!chartPanel.gameObject.activeSelf, true));

		gapSlider = MakeSlider(bar, 300, 2000, gapMs, out gapLabel, ref x);
		dashSlider = MakeSlider(bar, 100, 500, dashMs, out dashLabel, ref x);
		dashSlider.onValueChanged.AddListener(v =>
		{
			telegraph.longPressThreshold = v / 1000f;
			PlayerPrefs.SetInt(PrefDashMs, Mathf.RoundToInt(v));
			dashChanged = true;
		});
		gapSlider.onValueChanged.AddListener(v =>
		{
			telegraph.LetterGap = v / 1000f;
			PlayerPrefs.SetInt(PrefGapMs, Mathf.RoundToInt(v));
			gapChanged = true;
		});
	}

	Button MakeButton(RectTransform bar, string text, float width, ref float rightX)
	{
		var bg = Panel(text + " Button", bar, Surface, true);
		Place(bg.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-rightX, 0), new Vector2(width, 40));
		var button = bg.gameObject.AddComponent<Button>();
		button.transition = Selectable.Transition.None; // no extra click animation
		button.targetGraphic = bg;
		var label = Label("Label", bg.rectTransform, text, 18, TextCol, TextAlignmentOptions.Center);
		Stretch(label.rectTransform);
		rightX += width + 12;
		return button;
	}

	Slider MakeSlider(RectTransform bar, float min, float max, float value, out TextMeshProUGUI label, ref float rightX)
	{
		const float width = 150, labelWidth = 150;

		var rt = Rect("Slider", bar);
		Place(rt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-rightX, 0), new Vector2(width, 32));
		var hit = rt.gameObject.AddComponent<Image>();
		hit.color = new Color(0, 0, 0, 0); // invisible hit area so the whole strip is draggable

		var track = Panel("Track", rt, Line);
		Place(track.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 4));

		var fillArea = Rect("Fill Area", rt);
		Place(fillArea, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-14, 4));
		var fill = Panel("Fill", fillArea, Accent);
		fill.rectTransform.sizeDelta = Vector2.zero;

		var handleArea = Rect("Handle Area", rt);
		Place(handleArea, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-14, 16));
		var handle = Panel("Handle", handleArea, TextCol, true);
		handle.rectTransform.sizeDelta = new Vector2(16, 0);

		var slider = rt.gameObject.AddComponent<Slider>();
		slider.transition = Selectable.Transition.None;
		slider.targetGraphic = handle;
		slider.fillRect = fill.rectTransform;
		slider.handleRect = handle.rectTransform;
		slider.direction = Slider.Direction.LeftToRight;
		slider.minValue = min;
		slider.maxValue = max;
		slider.wholeNumbers = true;
		slider.value = Mathf.Clamp(value, min, max);

		rightX += width + 8;
		label = Label("Value", bar, "", 16, TextCol, TextAlignmentOptions.Right);
		Place(label.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-rightX, 0), new Vector2(labelWidth, 32));
		rightX += labelWidth + 20;
		return slider;
	}

	void BuildGauge()
	{
		var gauge = Rect("Gauge", stage);
		Place(gauge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(420, 8));
		var track = Panel("Track", gauge, Line);
		Stretch(track.rectTransform);

		gaugeFillImg = Panel("Fill", gauge, Dim);
		gaugeFill = gaugeFillImg.rectTransform;
		Place(gaugeFill, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 0));

		var line = Panel("Hairline", gauge, TextCol);
		line.sprite = null;
		gaugeLine = line.rectTransform;
		Place(gaugeLine, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2, 24));

		gaugeLabel = Label("Threshold", gauge, "", 14, Dim, TextAlignmentOptions.Center);
		Place(gaugeLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(120, 20));
	}

	void BuildChart()
	{
		var bg = Panel("Chart", root, Surface, true); // raycast target: clicks on the chart aren't presses
		chartPanel = bg.rectTransform;
		Place(chartPanel, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, -10), new Vector2(288, 560));

		var grid = Rect("Grid", chartPanel);
		Stretch(grid, 14);
		var layout = chartGrid = grid.gameObject.AddComponent<GridLayoutGroup>();
		layout.cellSize = new Vector2(84, 40);
		layout.spacing = new Vector2(4, 4);
		layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		layout.constraintCount = 3;

		const string order = "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
		var codeOf = new Dictionary<string, string>();
		foreach (var kv in TelegraphSystem.Codes) codeOf[kv.Value] = kv.Key;

		foreach (char c in order)
		{
			string code = codeOf[c.ToString()];
			var cell = Rect(c.ToString(), grid);
			var letter = Label("Letter", cell, c.ToString(), 22, TextCol, TextAlignmentOptions.Left);
			Place(letter.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(24, 0));
			var morse = Label("Code", cell, code, 18, Dim, TextAlignmentOptions.Left);
			morse.characterSpacing = 10;
			Place(morse.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(-30, 0));
			chartCells.Add(new ChartCell { code = code, letter = letter, morse = morse });
		}

		SetChartOpen(PlayerPrefs.GetInt(PrefChart, 1) == 1, false);
	}

	void BuildCaption()
	{
		captionRow = Rect("Tutorial", stage);
		Place(captionRow, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -190), new Vector2(-80, 44));
		caption = Label("Caption", captionRow, "", 26, Accent, TextAlignmentOptions.Center);
		Stretch(caption.rectTransform);

		var skipBg = Panel("Skip", captionRow, Surface, true);
		Place(skipBg.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(80, 32));
		var skip = skipBg.gameObject.AddComponent<Button>();
		skip.transition = Selectable.Transition.None;
		skip.targetGraphic = skipBg;
		var skipLabel = Label("Label", skipBg.rectTransform, "Skip", 16, Dim, TextAlignmentOptions.Center);
		Stretch(skipLabel.rectTransform);
		skip.onClick.AddListener(FinishTutorial);

		captionRow.gameObject.SetActive(false);
	}

	void BuildHint()
	{
		hint = Label("Hint", root, "tap = dot     hold = dash     pause = letter     .-.-.- = done     Esc = clear", 15, Dim, TextAlignmentOptions.Center);
		Place(hint.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(0, 24));
	}

	// ---------------------------------------------------------------- frame

	void Update()
	{
		ApplyLayout(false);
		UpdateStartOverlay();
		UpdateSettingsLabels();
		UpdateGauge();
		UpdateChartFilter();

		// Play the related sound once after a setting changes, when the drag is released.
		bool pointerHeld = Input.GetMouseButton(0) || Input.touchCount > 0;
		if (!pointerHeld && tutorial == null && demo == null)
		{
			if (dashChanged) { dashChanged = false; demo = StartCoroutine(DemoPress(telegraph.longPressThreshold * 1.6f, "-")); }
			else if (gapChanged) { gapChanged = false; demo = StartCoroutine(DemoGap()); }
		}
	}

	void UpdateSettingsLabels()
	{
		int dashMs = Mathf.RoundToInt(telegraph.longPressThreshold * 1000f);
		if (dashMs != shownDashMs)
		{
			shownDashMs = dashMs;
			dashLabel.text = $"Dash after <color=#{ColorUtility.ToHtmlStringRGB(Accent)}>{dashMs} ms</color>";
			gaugeLabel.text = $"{dashMs} ms";
		}
		int gapMs = Mathf.RoundToInt(telegraph.LetterGap * 1000f);
		if (gapMs != shownGapMs)
		{
			shownGapMs = gapMs;
			gapLabel.text = $"Letter gap <color=#{ColorUtility.ToHtmlStringRGB(Accent)}>{gapMs / 1000f:0.0} s</color>";
		}
	}

	void UpdateGauge()
	{
		float threshold = telegraph.longPressThreshold;
		float max = Mathf.Max(threshold * 2.5f, 0.6f);
		float width = ((RectTransform)gaugeFill.parent).rect.width;

		float elapsed = demoPress >= 0 ? demoPress : telegraph.PressElapsed;
		gaugeFill.sizeDelta = new Vector2(width * Mathf.Clamp01(elapsed / max), 0);
		gaugeFillImg.color = elapsed > threshold ? Accent : Dim;
		gaugeLine.anchoredPosition = new Vector2(width * threshold / max, 0);
	}

	void UpdateChartFilter()
	{
		if (!chartPanel.gameObject.activeSelf) return;
		string filter = telegraph.CurrentCode;
		if (filter == chartFilter) return;
		chartFilter = filter;

		foreach (var cell in chartCells)
		{
			bool any = string.IsNullOrEmpty(filter);
			bool match = !any && cell.code.StartsWith(filter, System.StringComparison.Ordinal);
			float alpha = any || match ? 1f : 0.2f;
			cell.letter.color = WithAlpha(match ? Accent : TextCol, alpha);
			cell.morse.color = WithAlpha(match ? TextCol : Dim, alpha);
		}
	}

	void SetChartOpen(bool open, bool remember)
	{
		chartPanel.gameObject.SetActive(open);
		chartFilter = null;
		if (portraitApplied.HasValue) ApplyLayout(true);
		if (chartButtonBg != null) chartButtonBg.color = open ? Line : Surface;
		if (chartButtonLabel != null) chartButtonLabel.color = open ? Accent : TextCol;
		if (remember) PlayerPrefs.SetInt(PrefChart, open ? 1 : 0);
	}

	bool IsOverControl(Vector2 screenPos)
	{
		var es = EventSystem.current;
		if (es == null) return false;
		var data = new PointerEventData(es) { position = screenPos };
		var hits = new List<RaycastResult>();
		raycaster.Raycast(data, hits);
		return hits.Count > 0;
	}

	// ---------------------------------------------------------------- web & touch

	void BuildTapKey()
	{
		if (!Application.isMobilePlatform && !Input.touchSupported) return;

		var bg = Panel("Tap Key", root, Surface, true);
		var label = Label("Label", bg.rectTransform, "KEY", 28, TextCol, TextAlignmentOptions.Center);
		label.characterSpacing = 20;
		Stretch(label.rectTransform);
		tapKey = bg.gameObject.AddComponent<MorseTapKey>();
		telegraph.ExtraPressed = () => tapKey.Held;
	}

	void BuildStartOverlay()
	{
		if (Application.platform != RuntimePlatform.WebGLPlayer) return;

		var bg = Panel("Start", root, WithAlpha(Bg, 0.92f));
		bg.sprite = null;
		startOverlay = bg.rectTransform;
		Stretch(startOverlay);
		string verb = tapKey != null ? "Tap" : "Press any key";
		var label = Label("Label", startOverlay, verb + " to start", 32, Accent, TextAlignmentOptions.Center);
		Stretch(label.rectTransform);

		// First-time players: the start press only opens the tutorial.
		// Returning players: it's not a raycast target, so the same press is the first dot.
		telegraph.BlockInput = PlayerPrefs.GetInt(PrefOnboarded, 0) == 0;
	}

	void UpdateStartOverlay()
	{
		if (startOverlay == null) return;
		bool pressed = Input.anyKeyDown || (tapKey != null && tapKey.Held);
		if (!pressed) return;

		Destroy(startOverlay.gameObject);
		startOverlay = null;
		if (PlayerPrefs.GetInt(PrefOnboarded, 0) == 0)
		{
			StartTutorial();
		}
		else
		{
			telegraph.BlockInput = false;
		}
	}

	/// <summary>Landscape: settings in one row, chart on the right.
	/// Portrait: settings in two rows, chart as a bottom sheet, on-screen key at the bottom.</summary>
	void ApplyLayout(bool force)
	{
		var screen = new Vector2Int(Screen.width, Screen.height);
		if (!force && screen == lastScreen) return;
		lastScreen = screen;
		bool portrait = Screen.height > Screen.width;
		portraitApplied = portrait;

		bool chartOpen = chartPanel.gameObject.activeSelf;
		float keyArea = 0;

		if (portrait)
		{
			scaler.referenceResolution = new Vector2(720, 1280);
			scaler.matchWidthOrHeight = 0f;

			Place(settingsBar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(-48, 96));
			Place(titleRt, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(260, 40));
			Place(helpRt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(44, 40));
			Place(chartButtonRt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-56, 0), new Vector2(88, 40));
			// Second row, left to right: label, slider, label, slider.
			PlaceRow2(dashLabel.rectTransform, 0, 150, TextAlignmentOptions.Left);
			PlaceRow2((RectTransform)dashSlider.transform, 156, 150, TextAlignmentOptions.Left);
			PlaceRow2(gapLabel.rectTransform, 336, 150, TextAlignmentOptions.Left);
			PlaceRow2((RectTransform)gapSlider.transform, 492, 150, TextAlignmentOptions.Left);

			if (tapKey != null)
			{
				Place((RectTransform)tapKey.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 56), new Vector2(-48, 140));
				keyArea = 56 + 140;
			}

			Place(chartPanel, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, keyArea + 16), new Vector2(-48, 300));
			chartGrid.cellSize = new Vector2(104, 40);
			chartGrid.constraintCount = 6;

			stage.offsetMin = new Vector2(0, keyArea + (chartOpen ? 316 : 0));
			stage.offsetMax = new Vector2(0, -112);
			hint.gameObject.SetActive(false); // the tutorial and chart cover it on small screens
		}
		else
		{
			scaler.referenceResolution = new Vector2(1280, 720);
			scaler.matchWidthOrHeight = 0.5f;

			Place(settingsBar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(-48, 44));
			Place(titleRt, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(260, 0));
			// Right-aligned row, right to left (same order as BuildSettingsBar).
			PlaceRow1(helpRt, 0, 44);
			PlaceRow1(chartButtonRt, 56, 88);
			PlaceRow1((RectTransform)gapSlider.transform, 156, 150);
			PlaceRow1(gapLabel.rectTransform, 314, 150);
			PlaceRow1((RectTransform)dashSlider.transform, 484, 150);
			PlaceRow1(dashLabel.rectTransform, 642, 150);
			dashLabel.alignment = TextAlignmentOptions.Right;
			gapLabel.alignment = TextAlignmentOptions.Right;

			if (tapKey != null)
			{
				Place((RectTransform)tapKey.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 48), new Vector2(320, 96));
				keyArea = 48 + 96;
			}

			Place(chartPanel, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, -10), new Vector2(288, 560));
			chartGrid.cellSize = new Vector2(84, 40);
			chartGrid.constraintCount = 3;

			stage.offsetMin = new Vector2(0, keyArea);
			stage.offsetMax = new Vector2(chartOpen ? -(288 + 48) : 0, 0);
			hint.gameObject.SetActive(tapKey == null);
		}
	}

	void PlaceRow1(RectTransform rt, float rightOffset, float width)
	{
		Place(rt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-rightOffset, 0), new Vector2(width, rt.sizeDelta.y));
	}

	void PlaceRow2(RectTransform rt, float leftOffset, float width, TextAlignmentOptions align)
	{
		Place(rt, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(leftOffset, 4), new Vector2(width, 32));
		var text = rt.GetComponent<TextMeshProUGUI>();
		if (text != null) text.alignment = align;
	}

	// ---------------------------------------------------------------- tutorial

	void StartTutorial()
	{
		if (tutorial != null) StopCoroutine(tutorial);
		tutorial = StartCoroutine(Tutorial());
	}

	IEnumerator Tutorial()
	{
		captionRow.gameObject.SetActive(true);
		telegraph.ClearSentence();

		// Each step: one line, an automatic demo once, then the player does it.
		caption.text = "Tap = dot";
		yield return DemoPress(0.08f, ".");
		sawDot = false;
		while (!sawDot) yield return null;

		caption.text = "Hold past the line = dash";
		yield return DemoPress(telegraph.longPressThreshold * 1.8f, "-");
		sawDash = false;
		while (!sawDash) yield return null;

		caption.text = "Pause = letter";
		telegraph.ClearSentence();
		yield return DemoGap();
		sawLetter = false;
		while (!sawLetter) yield return null;

		caption.text = "Stuck? The chart narrows as you type";
		SetChartOpen(false, false);
		yield return new WaitForSecondsRealtime(0.6f);
		SetChartOpen(true, true);
		yield return new WaitForSecondsRealtime(2.4f);

		FinishTutorial();
	}

	void FinishTutorial()
	{
		if (tutorial != null) StopCoroutine(tutorial);
		tutorial = null;
		StopDemo();
		captionRow.gameObject.SetActive(false);
		caption.text = "";
		telegraph.ClearSentence();
		PlayerPrefs.SetInt(PrefOnboarded, 1);
		PlayerPrefs.Save();
	}

	/// <summary>Presses the key by itself once: gauge fills, beep plays, input is held off meanwhile.</summary>
	IEnumerator DemoPress(float duration, string symbol)
	{
		telegraph.BlockInput = true;
		yield return new WaitForSecondsRealtime(0.4f);
		telegraph.beepSound.Play();
		float t = 0;
		while (t < duration)
		{
			demoPress = t;
			t += Time.unscaledDeltaTime;
			yield return null;
		}
		demoPress = duration;
		telegraph.beepSound.Stop();
		telegraph.liveMorseCodeText.text = symbol;
		yield return new WaitForSecondsRealtime(0.6f);
		telegraph.liveMorseCodeText.text = "";
		demoPress = -1f;
		telegraph.BlockInput = false;
		demo = null;
	}

	/// <summary>A dot, then the letter gap passes and the letter appears.</summary>
	IEnumerator DemoGap()
	{
		telegraph.BlockInput = true;
		yield return new WaitForSecondsRealtime(0.4f);
		telegraph.beepSound.Play();
		demoPress = 0.08f;
		yield return new WaitForSecondsRealtime(0.08f);
		telegraph.beepSound.Stop();
		telegraph.liveMorseCodeText.text = ".";
		yield return new WaitForSecondsRealtime(telegraph.LetterGap);
		telegraph.liveMorseCodeText.text = "";
		telegraph.feedbackText.text = "E";
		yield return new WaitForSecondsRealtime(0.8f);
		telegraph.feedbackText.text = "";
		demoPress = -1f;
		telegraph.BlockInput = false;
		demo = null;
	}

	void StopDemo()
	{
		if (demo != null) StopCoroutine(demo);
		demo = null;
		demoPress = -1f;
		telegraph.beepSound.Stop();
		telegraph.BlockInput = false;
	}

	// ---------------------------------------------------------------- helpers

	static Color Hex(string hex)
	{
		ColorUtility.TryParseHtmlString(hex, out Color c);
		return c;
	}

	static Color WithAlpha(Color c, float a)
	{
		c.a = a;
		return c;
	}

	static RectTransform Rect(string name, Transform parent)
	{
		var go = new GameObject(name, typeof(RectTransform));
		go.transform.SetParent(parent, false);
		return (RectTransform)go.transform;
	}

	Image Panel(string name, Transform parent, Color color, bool raycast = false)
	{
		var rt = Rect(name, parent);
		var img = rt.gameObject.AddComponent<Image>();
		img.sprite = rounded;
		img.type = Image.Type.Sliced;
		img.color = color;
		img.raycastTarget = raycast;
		return img;
	}

	static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align)
	{
		var rt = Rect(name, parent);
		var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
		label.text = text;
		label.fontSize = size;
		label.color = color;
		label.alignment = align;
		label.enableWordWrapping = false;
		label.raycastTarget = false;
		return label;
	}

	static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
	{
		rt.anchorMin = anchorMin;
		rt.anchorMax = anchorMax;
		rt.pivot = pivot;
		rt.anchoredPosition = pos;
		rt.sizeDelta = size;
	}

	static void Stretch(RectTransform rt, float inset = 0)
	{
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.offsetMin = new Vector2(inset, inset);
		rt.offsetMax = new Vector2(-inset, -inset);
	}

	static Sprite MakeRoundedSprite(int size, int radius)
	{
		var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
		var pixels = new Color32[size * size];
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius), 0);
				float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius), 0);
				float d = Mathf.Sqrt(dx * dx + dy * dy);
				byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255);
				pixels[y * size + x] = new Color32(255, 255, 255, a);
			}
		}
		tex.SetPixels32(pixels);
		tex.Apply();
		return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0,
			SpriteMeshType.FullRect, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
	}
}
