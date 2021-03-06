﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    private bool Paused = false;
    private bool Slowed = false;
    public float SlowMotionValue = 0.1f;

    // Update is called once per frame
    void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7))
        {
            if (Paused)
            {
                ResumeGame();
                Paused = false;
            }
            else
            {
                PauseGame();
                Paused = true;
            }
        }

        if (Input.GetKeyUp(KeyCode.F) || Input.GetKeyDown(KeyCode.JoystickButton2))
        {
            if (Slowed)
            {
                ResumeGame();
                Slowed = false;
            }
            else
            {
                Slowmotion(SlowMotionValue);
                Slowed = true;
            }
        }

        if (Input.GetAxis("Restart")!= 0)
        {
            ReloadScene();
            ResumeGame();
            Slowed = false;
            Paused = false;
        }
    }

    public void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void PauseGame()
    {
        Time.timeScale = 0;
    }

    public void ResumeGame()
    {
        Time.timeScale = 1;
    }

    public void Slowmotion(float Scale)
    {
        Time.timeScale = Scale;
    }

}
