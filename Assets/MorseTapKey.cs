using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>On-screen telegraph key for touch screens: held while a finger is down on it.</summary>
public class MorseTapKey : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
	int pointers;

	public bool Held => pointers > 0;

	public void OnPointerDown(PointerEventData eventData)
	{
		pointers++;
	}

	public void OnPointerUp(PointerEventData eventData)
	{
		pointers = Mathf.Max(0, pointers - 1);
	}

	void OnDisable()
	{
		pointers = 0;
	}
}
