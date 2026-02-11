using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// Desplaza el offset de una textura URP/Lit (_BaseMap) a velocidad constante.
/// Funciona en Play y en el Editor (sin Play) gracias a ExecuteAlways.
/// En Editor usa MaterialPropertyBlock para no modificar materiales ni assets.
[ExecuteAlways]
[DisallowMultipleComponent]
public class URPTextureOffsetScroller : MonoBehaviour
{
    [Tooltip("Renderer objetivo (si se deja vacío, se usa el del GameObject).")]
    public Renderer target;

    [Tooltip("Propiedad de textura a desplazar (URP/Lit usa _BaseMap).")]
    public string textureProperty = "_BaseMap";

    [Tooltip("Velocidad de desplazamiento en UV por segundo.")]
    public Vector2 speed = new Vector2(0.2f, 0f);

    [Tooltip("Usar tiempo no escalado (ignora Time.timeScale).")]
    public bool unscaledTime = false;

    [Tooltip("Usar MaterialPropertyBlock (recomendado, no duplica materiales).")]
    public bool useMaterialPropertyBlock = true;

    // --- Internos ---
    MaterialPropertyBlock _mpb;
    int _propId;
    int _stId;

    Vector2 _startOffset;   // offset inicial leído del material
    Vector2 _tiling = Vector2.one;

#if UNITY_EDITOR
    double _editorStartTime;
#endif

    void OnEnable()
    {
        if (!target) target = GetComponent<Renderer>();
        if (!target) { enabled = false; return; }

        _propId = Shader.PropertyToID(textureProperty);
        _stId = Shader.PropertyToID(textureProperty + "_ST");

        // Leer tiling/offset iniciales
        var mat = Application.isPlaying || !useMaterialPropertyBlock
            ? target.sharedMaterial // lectura segura
            : target.sharedMaterial;

        if (mat != null)
        {
            // _ST = (tiling.x, tiling.y, offset.x, offset.y)
            if (mat.HasProperty(_stId))
            {
                Vector4 st = mat.GetVector(_stId);
                _tiling = new Vector2(st.x == 0 ? 1f : st.x, st.y == 0 ? 1f : st.y);
                _startOffset = new Vector2(st.z, st.w);
            }
            else if (mat.HasProperty(_propId))
            {
                // Fallback para shaders que exponen GetTextureScale/Offset
                _tiling = mat.GetTextureScale(_propId);
                if (_tiling == Vector2.zero) _tiling = Vector2.one;
                _startOffset = mat.GetTextureOffset(_propId);
            }
        }

        if (_mpb == null) _mpb = new MaterialPropertyBlock();

#if UNITY_EDITOR
        _editorStartTime = EditorApplication.timeSinceStartup;
        // Asegura repintados regulares en escena mientras está activo en modo edición
        EditorApplication.update -= EditorUpdate;
        EditorApplication.update += EditorUpdate;
#endif
    }

    void OnDisable()
    {
        // Limpia el MPB para dejar el renderer con sus valores por defecto
        if (target)
        {
            if (useMaterialPropertyBlock)
                target.SetPropertyBlock(null);
            else if (!Application.isPlaying)
            {
                // Evita modificar assets fuera de Play: no toques sharedMaterial.
                // (Si el usuario forzó no-MPB, no revertimos en Edit para no ensuciar)
            }
        }

#if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
        // Último repintado para reflejar el clear del MPB
        SceneView.RepaintAll();
#endif
    }

#if UNITY_EDITOR
    // En modo edición, Update no siempre “latea”; forzamos avance con el update del editor
    void EditorUpdate()
    {
        if (!Application.isPlaying && this && enabled && gameObject.activeInHierarchy)
        {
            Tick((float)(EditorApplication.timeSinceStartup - _editorStartTime));
            // Repinta escena/inspector para ver el movimiento
            SceneView.RepaintAll();
            RepaintGameView();
        }
    }

    static void RepaintGameView()
    {
        // Repaint de GameView si estuviera abierta (opcional, seguro)
        var type = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        if (type != null)
        {
            var w = EditorWindow.GetWindow(type, false, null, false);
            if (w) w.Repaint();
        }
    }
#endif

    void Update()
    {
        if (!Application.isPlaying) return; // En editor sin Play se gestiona en EditorUpdate

        float t;
        if (unscaledTime) t = Time.unscaledTime;
        else t = Time.time;

        Tick(t);
    }

    void Tick(float tSeconds)
    {
        if (!target) return;

        Vector2 o = _startOffset + speed * tSeconds;
        o.x = Mathf.Repeat(o.x, 1f);
        o.y = Mathf.Repeat(o.y, 1f);

        if (useMaterialPropertyBlock || !Application.isPlaying)
        {
            // Editor: siempre MPB para no ensuciar materiales
            target.GetPropertyBlock(_mpb);
            _mpb.SetVector(_stId, new Vector4(_tiling.x, _tiling.y, o.x, o.y));
            target.SetPropertyBlock(_mpb);
        }
        else
        {
            // Runtime y el usuario eligió modificar la instancia de material
            var mat = target.material; // instancia en runtime
            if (mat != null)
            {
                if (mat.HasProperty(_propId))
                {
                    mat.SetTextureScale(_propId, _tiling);
                    mat.SetTextureOffset(_propId, o);
                }
                else if (mat.HasProperty(_stId))
                {
                    mat.SetVector(_stId, new Vector4(_tiling.x, _tiling.y, o.x, o.y));
                }
            }
        }
    }
}
