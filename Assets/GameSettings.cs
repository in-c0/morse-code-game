using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameSettings : MonoBehaviour
{
	public Slider speedSlider;
	public void SetGameSpeed()
	{
		Time.timeScale = speedSlider.value;
	}
}
