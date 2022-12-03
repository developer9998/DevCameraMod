using OVR.OpenVR;
using Photon.Pun;
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace DevCameraMod
{
    public class TagObject : MonoBehaviour
    {
        public VRRig rig;
        public TagSpawner tagSpawner;
        public GameObject tagObject;
        public Text tex;
        public Canvas canv;

        public void StartDelay() => Invoke("StartImmediate", 2);

        public void StartImmediate()
        {
            tagObject = Instantiate(Plugin.Instance.nametagBase);
            tex = tagObject.GetComponentInChildren<Text>();
            canv = tagObject.GetComponent<Canvas>();
            tagObject.AddComponent<TagDespawn>().rig = rig;
        }

        public void LateUpdate()
        {
            if (tagObject != null)
            {
                tagObject.transform.position = rig.headMesh.transform.position + new Vector3(0, 0.364f, 0);
                tagObject.transform.rotation = Plugin.Instance.camera.transform.rotation;
                tex.text = rig.playerText.text;
                tex.color = rig.setMatIndex == 0 ? rig.materialsToChangeTo[0].color : new Color(0.4588235f, 0.1098039f, 0);

                if (PhotonNetwork.InRoom && rig.isOfflineVRRig) canv.enabled = false;
                else if (!PhotonNetwork.InRoom & rig.isOfflineVRRig) canv.enabled = true;
            }
        }
    }
}
