using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>On-screen telegraph key for touch screens: held while a finger is down on it.</summary>
public class MorseTapKey : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
	int pointers;

	// Also require a finger/button to actually be down, so a lost pointer-up can't leave the key stuck.
	public bool Held => pointers > 0 && (Input.touchCount > 0 || Input.GetMouseButton(0));

	public void OnPointerDown(PointerEventData eventData)
	{
		pointers++;
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		pointers = Mathf.Max(0, pointers - 1);
	}

	void Update()
	{
		if (pointers > 0 && Input.touchCount == 0 && !Input.GetMouseButton(0))
		{
			pointers = 0;
		}
	}

	void OnApplicationFocus(bool focused)
	{
		if (!focused) pointers = 0;
	}

	void OnDisable()
	{
		pointers = 0;
	}
}
