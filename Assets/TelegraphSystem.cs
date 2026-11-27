using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

using UnityEngine.UI; // Include the UI namespace for Slider

public class TelegraphSystem : MonoBehaviour
{
	public AudioSource beepSound;
	public TextMeshProUGUI feedbackText;
	public TextMeshProUGUI collectionText;
	public TextMeshProUGUI liveMorseCodeText;
	public GameObject morseCodePrefab;
	public float longPressThreshold = 0.2f;
	private float keyDownTime;
	private float lastPressDuration;
	private string currentMorseCode = "";
	private bool isKeyDown = false;
	private bool inputLocked = false;
	private bool needRelease = false;
	private float needReleaseSince;
	private bool mouseStartedOnControl = false;
	private bool sentenceFinished = false;
	private Coroutine endOfInputCoroutine;
	private float endOfInputDelay = 1f;
	private float wordGapDelay = 1.5f; // extra pause after a letter that inserts a space
	private string decodedString = "";

	// Text colours (rich-text tags), tuned for a dark background.
	public static string ConfirmedColor = "#D9E0DB"; // letters already committed
	public static string EditingColor = "#7CFFB2";   // letter being typed
	public static string FinishedColor = "#7CFFB2";  // finished sentence stays on screen
	public static string ErrorColor = "#FF6B6B";

	public Slider thresholdSlider; // Legacy slider; the HUD clears this and sets the threshold directly.

	// ---- Hooks for the HUD -------------------------------------------------
	/// <summary>While true, presses are ignored (tutorial demos, start screen).</summary>
	public bool BlockInput { get; set; }
	/// <summary>Extra press source, e.g. the on-screen key.</summary>
	public Func<bool> ExtraPressed;
	/// <summary>Returns true when a screen position is over a UI control; clicks there are not telegraph presses.</summary>
	public Func<Vector2, bool> IsOverControl;

	public event Action<char> SymbolEntered;
	public event Action<string> LetterCommitted;

	public bool IsKeyDown => isKeyDown;
	/// <summary>Seconds the key has been held, or the last press length after release.</summary>
	public float PressElapsed => isKeyDown ? Time.unscaledTime - keyDownTime : lastPressDuration;
	public string CurrentCode => currentMorseCode;
	public float LetterGap { get => endOfInputDelay; set => endOfInputDelay = value; }
	// ------------------------------------------------------------------------

	void Update()
	{
		if (thresholdSlider != null)
		{
			// Dynamically update longPressThreshold based on the slider's value
			longPressThreshold = Mathf.Lerp(0.1f, 0.5f, (thresholdSlider.value - 0.5f) / (3f - 0.5f));
		}

		if (Input.GetKeyDown(KeyCode.Escape) && !inputLocked && !BlockInput)
		{
			ClearSentence();
			return;
		}

		bool pressed = ReadPressed();

		if (inputLocked || BlockInput)
		{
			CancelPress();
			RequireRelease();
			return;
		}

		// After a block ends, wait until everything is released before accepting a new press.
		// The timeout covers a key-up the browser never delivered (e.g. focus lost mid-press).
		if (needRelease)
		{
			if (pressed && Time.unscaledTime - needReleaseSince < 1.5f) return;
			needRelease = false;
		}

		if (pressed)
		{
			if (!isKeyDown)
			{
				isKeyDown = true;
				keyDownTime = Time.unscaledTime;
				beepSound.Play();

				// A new key press after a full stop starts a new sentence.
				if (sentenceFinished)
				{
					BeginNewSentence();
				}

				feedbackText.text = "";
				StopEndOfInputTimer();
			}
		}
		else if (isKeyDown)
		{
			isKeyDown = false;
			beepSound.Stop();
			lastPressDuration = Time.unscaledTime - keyDownTime;
			char symbol = lastPressDuration <= longPressThreshold ? '.' : '-';
			currentMorseCode += symbol;

			UpdateLiveMorseCodeDisplay();
			SymbolEntered?.Invoke(symbol);

			// Commit the letter once the key has been released for endOfInputDelay.
			if (!sentenceFinished && !inputLocked)
			{
				endOfInputCoroutine = StartCoroutine(EndOfInputSequence());
			}
		}
	}

	private void RequireRelease()
	{
		needReleaseSince = Time.unscaledTime; // counts from the last blocked frame / focus loss
		needRelease = true;
	}

	void OnApplicationFocus(bool focused)
	{
		if (!focused)
		{
			CancelPress();
			RequireRelease();
		}
	}

	private static KeyCode[] keyboardKeys;

	// Input.anyKey also counts mouse buttons; when a mouse button is held, check the keyboard on its own.
	private static bool AnyKeyboardKeyHeld()
	{
		if (keyboardKeys == null)
		{
			var keys = new List<KeyCode>();
			foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
			{
				if (k == KeyCode.None || k == KeyCode.Escape) continue;
				if (k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6) continue;
				if (k >= KeyCode.JoystickButton0) continue;
				keys.Add(k);
			}
			keyboardKeys = keys.ToArray();
		}
		foreach (var k in keyboardKeys)
		{
			if (Input.GetKey(k)) return true;
		}
		return false;
	}

	private bool ReadPressed()
	{
		bool mouseHeld = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
		if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
		{
			mouseStartedOnControl = IsOverControl != null && IsOverControl(Input.mousePosition);
		}

		bool mouse = mouseHeld && !mouseStartedOnControl;
		bool keyboard = Input.anyKey && !Input.GetKey(KeyCode.Escape) && (!mouseHeld || AnyKeyboardKeyHeld());
		bool extra = ExtraPressed != null && ExtraPressed();
		return keyboard || mouse || extra;
	}

	private void CancelPress()
	{
		if (isKeyDown)
		{
			isKeyDown = false;
			beepSound.Stop();
		}
	}

	IEnumerator EndOfInputSequence()
	{
		yield return new WaitForSecondsRealtime(endOfInputDelay);
		DecodeMorseCode();

		// Keep pausing and the word ends: add a space automatically.
		yield return new WaitForSecondsRealtime(wordGapDelay);
		endOfInputCoroutine = null;
		if (!sentenceFinished && decodedString.Length > 0 && !decodedString.EndsWith(" "))
		{
			decodedString += " ";
			feedbackText.text = "spacebar";
			UpdateCollectionText();
		}
	}

	private void StopEndOfInputTimer()
	{
		if (endOfInputCoroutine != null)
		{
			StopCoroutine(endOfInputCoroutine);
			endOfInputCoroutine = null;
		}
	}

	private void DecodeMorseCode()
	{
		if (string.IsNullOrEmpty(currentMorseCode))
		{
			return;
		}

		string decodedLetter = MorseCodeToLetter(currentMorseCode);
		currentMorseCode = "";
		liveMorseCodeText.text = "";

		if (decodedLetter == "[?]")
		{
			// Unknown code: show an error but keep the sentence typed so far.
			feedbackText.text = $"<color={ErrorColor}>?</color>";
		}
		else if (decodedLetter == "WAIT")
		{
			// Prosign: acknowledged, but not added to the sentence.
			feedbackText.text = "WAIT";
		}
		else
		{
			decodedString += decodedLetter;
			feedbackText.text = decodedLetter == " " ? "spacebar" : decodedLetter;
		}

		UpdateCollectionText();
		LetterCommitted?.Invoke(decodedLetter);
	}

	private void UpdateCollectionText()
	{
		collectionText.text = $"<color={ConfirmedColor}>{decodedString}</color>";
	}

	private void UpdateLiveMorseCodeDisplay()
	{
		liveMorseCodeText.text = currentMorseCode;

		string liveLetter = MorseCodeToLetter(currentMorseCode);

		if (liveLetter == "STARTOVER")
		{
			StartCoroutine(StartOverMorseCode());
			return;
		}

		if (liveLetter == ".")
		{
			CompleteSentence();
			return;
		}

		string shownLetter;
		if (liveLetter == "[?]")
		{
			shownLetter = $"<color={ErrorColor}>?</color>";
		}
		else if (liveLetter == "WAIT")
		{
			shownLetter = "";
		}
		else
		{
			shownLetter = $"<color={EditingColor}>{liveLetter}</color>";
		}

		collectionText.text = $"<color={ConfirmedColor}>{decodedString}</color>" + shownLetter;
	}

	IEnumerator StartOverMorseCode()
	{
		StopEndOfInputTimer();
		inputLocked = true;
		CancelPress();

		collectionText.text = "ERROR. START OVER";
		decodedString = "";
		feedbackText.text = "";
		currentMorseCode = "";
		liveMorseCodeText.text = "";
		sentenceFinished = false;

		yield return new WaitForSecondsRealtime(3f);

		collectionText.text = "";
		inputLocked = false;
	}

	/// <summary>Clears the sentence and any half-typed letter immediately (Esc).</summary>
	public void ClearSentence()
	{
		StopEndOfInputTimer();
		CancelPress();
		decodedString = "";
		currentMorseCode = "";
		collectionText.text = "";
		feedbackText.text = "";
		liveMorseCodeText.text = "";
		sentenceFinished = false;
	}

	// Called when the full stop (.-.-.-) is typed: the finished sentence stays on screen
	// until the next key press starts a new one.
	private void CompleteSentence()
	{
		StopEndOfInputTimer();

		decodedString += ".";
		Debug.Log(decodedString);

		collectionText.text = $"<color={FinishedColor}>{decodedString}</color>";
		liveMorseCodeText.text = "";
		currentMorseCode = "";
		feedbackText.text = "";
		sentenceFinished = true;
	}

	private void BeginNewSentence()
	{
		decodedString = "";
		collectionText.text = "";
		sentenceFinished = false;
	}

	/// <summary>International Morse code table used for decoding and for the on-screen chart.</summary>
	public static readonly Dictionary<string, string> Codes = new Dictionary<string, string>
	{
		{ ".-", "A" }, { "-...", "B" }, { "-.-.", "C" }, { "-..", "D" }, { ".", "E" },
		{ "..-.", "F" }, { "--.", "G" }, { "....", "H" }, { "..", "I" }, { ".---", "J" },
		{ "-.-", "K" }, { ".-..", "L" }, { "--", "M" }, { "-.", "N" }, { "---", "O" },
		{ ".--.", "P" }, { "--.-", "Q" }, { ".-.", "R" }, { "...", "S" }, { "-", "T" },
		{ "..-", "U" }, { "...-", "V" }, { ".--", "W" }, { "-..-", "X" }, { "-.--", "Y" },
		{ "--..", "Z" },

		{ ".----", "1" }, { "..---", "2" }, { "...--", "3" }, { "....-", "4" }, { ".....", "5" },
		{ "-....", "6" }, { "--...", "7" }, { "---..", "8" }, { "----.", "9" }, { "-----", "0" },

		{ "-.-.--", "!" }, { "..--..", "?" }, { "--..--", "," }, { ".----.", "'" }, { ".-..-.", "\"" },
		{ "-..-.", "/" }, { "-.--.", "(" }, { "-.--.-", ")" }, { "---...", ":" }, { "-.-.-.", ";" },
		{ "-...-", "=" }, { ".-.-.", "+" }, { "-....-", "-" }, { "..--.-", "_" }, { ".--.-.", "@" },

		{ ".......", " " },
		{ "...-.-.-...", "STARTOVER" }, { "........", "STARTOVER" },
		{ ".-.-.-", "." },
		{ ".-...", "WAIT" }, { "..-...", "WAIT" },
	};

	private string MorseCodeToLetter(string morseCode)
	{
		return Codes.TryGetValue(morseCode, out string letter) ? letter : "[?]";
	}
}
