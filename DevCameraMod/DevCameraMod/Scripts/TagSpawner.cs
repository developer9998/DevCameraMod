using UnityEngine;

namespace DevCameraMod.Scripts
{
    public class TagSpawner : MonoBehaviour
    {
        public void Start()
        {
            VRRig rig = gameObject.GetComponent<VRRig>();
            TagObject obj = gameObject.AddComponent<TagObject>();
            obj.rig = rig;
            obj.tagSpawner = this;

            if (Plugin.Instance.nametagBase == null) obj.StartDelay(); else obj.StartImmediate();
        }
    }
}
