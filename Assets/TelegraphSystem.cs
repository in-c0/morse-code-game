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
	private string currentMorseCode = "";
	private bool isKeyDown = false;
	private Coroutine endOfInputCoroutine;
	private Coroutine fadeCoroutine;
	private float endOfInputDelay = 1f;
	private float fadeDelay = 2f;
	private float fadeDuration = 5f;
	private List<GameObject> morseCodeObjects = new List<GameObject>();
	private string decodedString = "";
	private bool fullStopUsed = false;

	public Slider thresholdSlider; // Reference to the slider controlling the long press threshold

	void Update()
	{
		if (thresholdSlider != null)
		{
			// Dynamically update longPressThreshold based on the slider's value
			longPressThreshold = Mathf.Lerp(0.1f, 0.5f, (thresholdSlider.value - 0.5f) / (3f - 0.5f));
		}
		if (Input.anyKey)
		{
			if (!isKeyDown)
			{
				isKeyDown = true;
				keyDownTime = Time.time;
				beepSound.Play();
				feedbackText.text = "loading ...";
				ResetFadeEffect();
			}
		}
		else if (isKeyDown)
		{
			isKeyDown = false;
			beepSound.Stop();
			float keyPressDuration = Time.time - keyDownTime;
			currentMorseCode += keyPressDuration <= longPressThreshold ? "." : "-";

			if(!fullStopUsed)
				StartFadeEffect();
	
			UpdateLiveMorseCodeDisplay();
		}
	}

	IEnumerator EndOfInputSequence()
	{
		yield return new WaitForSeconds(endOfInputDelay);
		DecodeMorseCode();
		currentMorseCode = "";
		liveMorseCodeText.text = "";
		StartFadeEffect();
	}

	private void DecodeMorseCode()
	{
		string decodedLetter = MorseCodeToLetter(currentMorseCode);
		if (!string.IsNullOrEmpty(decodedLetter) && (decodedLetter != "[?]"  || decodedLetter != "STAROVER"))
		{
			decodedString += decodedLetter;
			UpdateCollectionText(decodedString);
			feedbackText.text = decodedLetter == " " ? "spacebar" : $"{decodedLetter}";
			ClearMorseCodeObjects();
		}
		else
		{
			feedbackText.text = "<color=#FF0000>" + "?" + "</color>";
		}
	}

	private void UpdateCollectionText(string text)
	{
		collectionText.text = "<color=#004d00>" + text + "</color>"; // Dark green color for confirmed text
	}

	private void UpdateLiveMorseCodeDisplay()
	{
		liveMorseCodeText.text = currentMorseCode;

		string liveLetter = MorseCodeToLetter(currentMorseCode);
		if (liveLetter == "[?]")
		{
			liveLetter = "<color=#FF0000>" + "?" + "</color>";
		}

		collectionText.text = "<color=#004d00>" + decodedString + "</color>"; // Dark green color for confirmed text
		collectionText.text += "<color=#00ff00>" + liveLetter + "</color>"; // Green color for the current editing letter

		if (liveLetter == "STARTOVER")
		{
			StartCoroutine(StartOverMorseCode());
		} else if(liveLetter == ".")
		{
			// TODO: DISABLE INPUT
			fullStopUsed = true;
			CompleteSentence();
		}
	}
	IEnumerator StartOverMorseCode()
	{
		if (fadeCoroutine != null)
		{
			StopCoroutine(fadeCoroutine);
		}

		if (endOfInputCoroutine != null)
		{
			StopCoroutine(endOfInputCoroutine);
		}

		// To-do: Disable Input while starting over
		collectionText.text = "ERROR. START OVER";
		decodedString = "";
		feedbackText.text = "";
		currentMorseCode = "";
		liveMorseCodeText.text = "";
		ClearMorseCodeObjects();

		yield return new WaitForSeconds(3f);
		decodedString = "";
		collectionText.text = "";
		currentMorseCode = "";
		feedbackText.text = "";
		liveMorseCodeText.text = "";
		ClearMorseCodeObjects();

		// To-do: Re-enable Input aFter starting over
	}
	private void ClearMorseCodeObjects()
	{
		foreach (var morseObj in morseCodeObjects)
		{
			Destroy(morseObj);
		}
		morseCodeObjects.Clear();
	}

	private void StartFadeEffect()
	{
		if (fadeCoroutine != null)
		{
			StopCoroutine(fadeCoroutine);
		}
		fadeCoroutine = StartCoroutine(FadeCollectionText());
	}

	IEnumerator FadeCollectionText()
	{
		yield return new WaitForSeconds(fadeDelay);

		float elapsedTime = 0;
		while (elapsedTime < fadeDuration)
		{
			elapsedTime += Time.deltaTime;
			float alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
			SetTextOpacity(collectionText, alpha);
			yield return null;
		}

		CompleteSentence();
	}

	private void CompleteSentence()
	{
		if(endOfInputCoroutine != null) StopCoroutine(endOfInputCoroutine);
		if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

		Debug.Log(collectionText.text);

		decodedString = "";
		collectionText.text = "";
		liveMorseCodeText.text = "";
		currentMorseCode = "";
		feedbackText.text = "";
		SetTextOpacity(collectionText, 1f);
		ClearMorseCodeObjects();
		fullStopUsed = false;
	}

	private void ResetFadeEffect()
	{
		if (fadeCoroutine != null)
		{
			StopCoroutine(fadeCoroutine);
			SetTextOpacity(collectionText, 1f);
		}
		if (endOfInputCoroutine != null)
		{
			StopCoroutine(endOfInputCoroutine);
		}
		endOfInputCoroutine = StartCoroutine(EndOfInputSequence());
	}

	private void SetTextOpacity(TextMeshProUGUI text, float opacity)
	{
		Color color = text.color;
		color.a = opacity;
		text.color = color;
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

			case "-.-.--": return "!";
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
