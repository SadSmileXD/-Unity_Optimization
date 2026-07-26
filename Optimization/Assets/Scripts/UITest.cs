using TMPro;
using UnityEngine;

public class UITest : MonoBehaviour
{
    public TextMeshProUGUI test;
    

    // Update is called once per frame
    void Update()
    {
        test.text = "HP : " +
            "Time : " + Time.deltaTime;

    }
}
