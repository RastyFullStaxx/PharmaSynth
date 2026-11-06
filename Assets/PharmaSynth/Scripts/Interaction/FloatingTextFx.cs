using TMPro;
using UnityEngine;

/// Rise + fade driver for FloatingText (component so it survives the frame).
public class FloatingTextFx : MonoBehaviour
{
    private TextMeshPro _tmp;
    private float _age;
    private const float Life = 2.5f;
    private const float Rise = 0.15f;

    public void Bind(TextMeshPro tmp) => _tmp = tmp;

    private void Update()
    {
        _age += Time.deltaTime;
        transform.position += Vector3.up * (Rise / Life) * Time.deltaTime;
        if (_tmp != null)
        {
            float a = 1f - Mathf.SmoothStep(0.55f, 1f, _age / Life);
            var c = _tmp.color; c.a = a; _tmp.color = c;
        }
        if (_age >= Life) Destroy(gameObject);
    }
}
