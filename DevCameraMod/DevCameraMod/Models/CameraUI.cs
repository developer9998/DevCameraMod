using System;
using UnityEngine;
using UnityEngine.UI;

namespace DevCameraMod.Models
{
    [Serializable]
    public class CameraUI
    {
        public Canvas canvas;
        public Text cameraSpectator;
        public Text currentlySpectating;
        public RawImage currentSpecImage;
        public Text leftTeam;
        public Text rightTeam;
        public Text leftPoints;
        public Text rightPoints;
        public Text currentTime;
        public Text lapTime;
        public Text scoreboardText;
        public Text scoreboardText2;
        public double timeStamp;

        public string LeftTeamName = "null";
        public string RightTeamName = "null";

        public void AdjustTeam(bool add, bool team)
        {
            if (team)
            {
                int points = int.Parse(leftPoints.text);
                leftPoints.text = (points + (add ? 1 : -1)).ToString();
                return;
            }

            int pointsAlt = int.Parse(rightPoints.text);
            rightPoints.text = (pointsAlt + (add ? 1 : -1)).ToString();
        }
    }
}
