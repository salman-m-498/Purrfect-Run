using UnityEngine;

public class DodgeButtonToggle : MonoBehaviour
{
    [SerializeField] GameObject leftButton;
    [SerializeField] GameObject rightButton;

    void Start()
    {
        bool touch = Input.touchSupported && Application.isMobilePlatform;
        leftButton.SetActive(touch);
        rightButton.SetActive(touch);
    }
}