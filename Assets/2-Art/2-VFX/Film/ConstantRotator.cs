using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// Rota el objeto a velocidad constante (en modo Play y Editor).
[ExecuteAlways]
[DisallowMultipleComponent]
public class ConstantRotator : MonoBehaviour
{
    [Tooltip("Velocidad de rotación en grados por segundo.")]
    public Vector3 rotationSpeed = new Vector3(0f, 90f, 0f);

    [Tooltip("Usar tiempo real del editor (sin Time.time).")]
    public bool useEditorTime = true;

    Vector3 _initialRotation;

#if UNITY_EDITOR
    double _editorStartTime;
#endif

    void OnEnable()
    {
        _initialRotation = transform.localEulerAngles;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            _editorStartTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= EditorUpdate;
            EditorApplication.update += EditorUpdate;
        }
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.update -= EditorUpdate;
            // Restaurar rotación inicial
            transform.localEulerAngles = _initialRotation;
            SceneView.RepaintAll();
        }
#endif
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        transform.Rotate(rotationSpeed * Time.deltaTime, Space.Self);
    }

#if UNITY_EDITOR
    void EditorUpdate()
    {
        if (Application.isPlaying || !this || !enabled || !gameObject.activeInHierarchy)
            return;

        double elapsed = EditorApplication.timeSinceStartup - _editorStartTime;
        transform.localEulerAngles = _initialRotation + rotationSpeed * (float)elapsed;
        SceneView.RepaintAll();
    }
#endif
}
