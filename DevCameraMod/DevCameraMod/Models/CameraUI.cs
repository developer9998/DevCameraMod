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
        public Text versionTex;
        public Text version2;
        public Text codeSecret;
        public Text scoreHeader;
        public double timeStamp;
        public Transform Sponsors;

        public string LeftTeamName = "left";
        public string RightTeamName = "right";

        public void AdjustTeam(bool add, bool team)
        {
            if (team)
            {
                int points = int.Parse(leftPoints.text);
                if (add) points++; else points--;
                leftPoints.text = points.ToString();
                return;
            }

            int pointsAlt = int.Parse(rightPoints.text);
            if (add) pointsAlt++; else pointsAlt--;
            rightPoints.text = pointsAlt.ToString();
        }
    }
}
