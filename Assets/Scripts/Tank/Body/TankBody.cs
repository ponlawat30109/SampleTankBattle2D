using UnityEngine;

public class TankBody : MonoBehaviour
{
    [field: SerializeField] public GameObject BodyTransform { get; set; }
    [field: SerializeField] public GameObject CannonTransform { get; set; }
    [field: SerializeField] public GameObject FirePointTransform { get; set; }

    [field: SerializeField] public Color[] TankColor { get; set; }
}
