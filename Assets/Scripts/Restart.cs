using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

public class Restart : MonoBehaviour
{
	public void RestartSameLevel()
	{
		RedDroneTactics.ResetBattleState();
		SceneManager.LoadScene(SceneManager.GetActiveScene().name);
	}
}
