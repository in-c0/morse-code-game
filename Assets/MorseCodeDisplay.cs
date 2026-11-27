using UnityEngine;
using TMPro;

public class MorseCodeDisplay : MonoBehaviour
{
	public GameObject textPrefab; // Assign your TextMeshProUGUI prefab here
	public Vector3 offset; // Offset from the parent object position

	public void CreateMorseCodeText(GameObject character, string morseCode)
	{
		// Instantiate the TextMeshProUGUI prefab
		GameObject textInstance = Instantiate(textPrefab, character.transform.position + offset, Quaternion.identity, character.transform);

		// Set the text to the Morse code
		TextMeshProUGUI textComponent = textInstance.GetComponent<TextMeshProUGUI>();
		textComponent.text = morseCode;

		// Adjust local position based on the offset
		textComponent.transform.localPosition = offset;
	}
}
