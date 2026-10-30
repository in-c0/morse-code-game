using System.Collections;
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
	private string currentMorseCode = "";
	private bool isKeyDown = false;
	private bool inputLocked = false;
	private bool sentenceFinished = false;
	private Coroutine endOfInputCoroutine;
	private float endOfInputDelay = 1f;
	private float wordGapDelay = 1.5f; // extra pause after a letter that inserts a space
	private string decodedString = "";

	private const string ConfirmedColor = "#004d00"; // dark green: letters already committed
	private const string EditingColor = "#00ff00";   // bright green: letter being typed
	private const string FinishedColor = "#00ff00";  // finished sentence stays on screen
	private const string ErrorColor = "#FF0000";

	public Slider thresholdSlider; // Reference to the slider controlling the long press threshold

	void Update()
	{
		if (thresholdSlider != null)
		{
			// Dynamically update longPressThreshold based on the slider's value
			longPressThreshold = Mathf.Lerp(0.1f, 0.5f, (thresholdSlider.value - 0.5f) / (3f - 0.5f));
		}

		if (inputLocked)
		{
			return;
		}

		if (Input.anyKey)
		{
			if (!isKeyDown)
			{
				isKeyDown = true;
				keyDownTime = Time.time;
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
			float keyPressDuration = Time.time - keyDownTime;
			currentMorseCode += keyPressDuration <= longPressThreshold ? "." : "-";

			UpdateLiveMorseCodeDisplay();

			// Commit the letter once the key has been released for endOfInputDelay.
			if (!sentenceFinished && !inputLocked)
			{
				endOfInputCoroutine = StartCoroutine(EndOfInputSequence());
			}
		}
	}

	IEnumerator EndOfInputSequence()
	{
		yield return new WaitForSeconds(endOfInputDelay);
		DecodeMorseCode();

		// Keep pausing and the word ends: add a space automatically.
		yield return new WaitForSeconds(wordGapDelay);
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
		beepSound.Stop();
		isKeyDown = false;

		collectionText.text = "ERROR. START OVER";
		decodedString = "";
		feedbackText.text = "";
		currentMorseCode = "";
		liveMorseCodeText.text = "";
		sentenceFinished = false;

		yield return new WaitForSeconds(3f);

		collectionText.text = "";
		inputLocked = false;
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

	private string MorseCodeToLetter(string morseCode)
	{
		switch (morseCode)
		{
			case ".-": return "A";
			case "-...": return "B";
			case "-.-.": return "C";
			case "-..": return "D";
			case ".": return "E";
			case "..-.": return "F";
			case "--.": return "G";
			case "....": return "H";
			case "..": return "I";
			case ".---": return "J";
			case "-.-": return "K";
			case ".-..": return "L";
			case "--": return "M";
			case "-.": return "N";
			case "---": return "O";
			case ".--.": return "P";
			case "--.-": return "Q";
			case ".-.": return "R";
			case "...": return "S";
			case "-": return "T";
			case "..-": return "U";
			case "...-": return "V";
			case ".--": return "W";
			case "-..-": return "X";
			case "-.--": return "Y";
			case "--..": return "Z";

			case ".----": return "1";
			case "..---": return "2";
			case "...--": return "3";
			case "....-": return "4";
			case ".....": return "5";
			case "-....": return "6";
			case "--...": return "7";
			case "---..": return "8";
			case "----.": return "9";
			case "-----": return "0";

			case "-.-.--": return "!";
			case "..--..": return "?";
			case "--..--": return ",";
			case ".----.": return "'";
			case ".-..-.": return "\"";
			case "-..-.": return "/";
			case "-.--.": return "(";
			case "-.--.-": return ")";
			case "---...": return ":";
			case "-.-.-.": return ";";
			case "-...-": return "=";
			case ".-.-.": return "+";
			case "-....-": return "-";
			case "..--.-": return "_";
			case ".--.-.": return "@";
			case ".......": return " ";

			case "...-.-.-...":
			case "........": return "STARTOVER";

			case ".-.-.-": return ".";

			case ".-...":
			case "..-...": return "WAIT";
			default: return "[?]";
		}
	}
}
