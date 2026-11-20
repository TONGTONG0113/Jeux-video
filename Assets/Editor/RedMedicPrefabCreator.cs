using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal static class RedMedicPrefabCreator
{
    const string PrefabPath = "Assets/Prefabs/MedicRed.prefab";
    const string DronePrefabPath = "Assets/Prefabs/DroneRed.prefab";
    const string MaterialsPath = "Assets/Materials/MedicRed";
    const float StartingHealth = 100f;

    [MenuItem("Tools/RED AI/Create Medic Prefab")]
    static void CreatePrefab()
    {
        Object existing = AssetDatabase.LoadMainAssetAtPath(PrefabPath);
        if (existing != null)
        {
            // Edit the existing contents so scene instances retain their prefab references and overrides.
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                UpdateHealthBar(contents.GetComponent<Health>());
                if (PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath) == null)
                {
                    throw new InvalidOperationException("Could not update the RED Medic prefab at " + PrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            SelectAsset(AssetDatabase.LoadMainAssetAtPath(PrefabPath));
            return;
        }

        Shader armorShader = Shader.Find("Standard");
        Shader effectShader = Shader.Find("Unlit/Color");
        if (armorShader == null || effectShader == null)
            throw new InvalidOperationException("The Medic generator requires the built-in Standard and Unlit/Color shaders.");

        if (!AssetDatabase.IsValidFolder(MaterialsPath))
            AssetDatabase.CreateFolder("Assets/Materials", "MedicRed");

        Material red = GetOrCreateMaterial("Red Armor", armorShader, new Color(0.76f, 0.045f, 0.075f));
        Material white = GetOrCreateMaterial("White Armor", armorShader, new Color(0.95f, 0.96f, 0.92f));
        Material dark = GetOrCreateMaterial("Charcoal Trim", armorShader, new Color(0.09f, 0.12f, 0.16f));
        Material light = GetOrCreateMaterial("Healing Light", effectShader, new Color(0.13f, 1f, 0.68f));

        var root = new GameObject("MedicRed") { tag = Medic.RedTeamTag };
        try
        {
            BuildRobot(root.transform, red, white, dark);
            ConfigureAgent(root.AddComponent<NavMeshAgent>());

            // The projectile code checks the hit object's tag and finds Health below that object.
            // A single root hitbox matches both conventions and avoids multiple explosion hits.
            var hitbox = root.AddComponent<CapsuleCollider>();
            hitbox.center = new Vector3(0f, 0.84f, 0f);
            hitbox.radius = 0.66f;
            hitbox.height = 1.68f;
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var health = root.AddComponent<Health>();
            var medic = root.AddComponent<Medic>();
            ConfigureHealth(health, medic);
            UpdateHealthBar(health);

            LineRenderer beam = AddEffect(root.transform, "Healing Beam", light,
                new Vector3(0f, 1.08f, 0.53f), 0.13f);
            beam.positionCount = 2;
            beam.useWorldSpace = true;

            LineRenderer ring = AddEffect(root.transform, "Healing Ring", light, Vector3.up * 0.12f, 0.075f);
            ring.positionCount = 32;
            ring.loop = true;
            ring.useWorldSpace = false;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = 2f * Mathf.PI * i / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.94f);
            }

            var medicSettings = new SerializedObject(medic);
            medicSettings.FindProperty("m_Health").objectReferenceValue = health;
            medicSettings.FindProperty("m_HealingBeam").objectReferenceValue = beam;
            medicSettings.FindProperty("m_HealingRing").objectReferenceValue = ring;
            medicSettings.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (prefab == null)
                throw new InvalidOperationException("Could not save the RED Medic prefab at " + PrefabPath);

            SelectAsset(prefab);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static void ConfigureAgent(NavMeshAgent agent)
    {
        agent.agentTypeID = 0;
        agent.radius = 0.35f;
        agent.height = 1.68f;
        agent.baseOffset = 0f;
        agent.speed = 12f;
        agent.acceleration = 16f;
        agent.angularSpeed = 360f;
        agent.stoppingDistance = 0.1f;
        agent.avoidancePriority = 75;
        agent.autoRepath = false;
    }

    static void ConfigureHealth(Health health, Medic medic)
    {
        var settings = new SerializedObject(health);
        settings.FindProperty("m_StartHealth").floatValue = StartingHealth;
        settings.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(health.DeathEvent, medic.Die);
        EditorUtility.SetDirty(health);
    }

    static void UpdateHealthBar(Health health)
    {
        var settings = new SerializedObject(health);
        SerializedProperty healthBar = settings.FindProperty("m_HealthBar");
        var oldSlider = healthBar.objectReferenceValue as Slider;
        Canvas oldCanvas = oldSlider != null ? oldSlider.GetComponentInParent<Canvas>() : null;
        if (oldSlider != null && (oldCanvas == null || oldCanvas.transform.parent != health.transform ||
            oldCanvas.name != "Health Bar"))
        {
            throw new InvalidOperationException("The RED Medic is wired to a health bar outside the generated hierarchy.");
        }

        GameObject drone = AssetDatabase.LoadAssetAtPath<GameObject>(DronePrefabPath);
        Health droneHealth = drone.GetComponent<Drone>().UnitHealth;
        Canvas canvas = droneHealth.GetComponentInChildren<Canvas>();
        GameObject bar = Object.Instantiate(canvas.gameObject, health.transform, false);
        bar.name = "Health Bar";
        Vector3 position = bar.transform.localPosition;
        position.y = 1.9f;
        bar.transform.localPosition = position;
        // The Drone's Canvas inherits scale from its Health parent.
        bar.transform.localScale = Vector3.Scale(canvas.transform.localScale, droneHealth.transform.lossyScale);

        if (oldCanvas != null)
            bar.transform.SetSiblingIndex(oldCanvas.transform.GetSiblingIndex());

        healthBar.objectReferenceValue = bar.GetComponentInChildren<Slider>();
        settings.ApplyModifiedPropertiesWithoutUndo();
        if (oldCanvas != null)
            Object.DestroyImmediate(oldCanvas.gameObject);
    }

    static void BuildRobot(Transform parent, Material red, Material white, Material dark)
    {
        AddPart(parent, "Chassis", PrimitiveType.Cube, new Vector3(0f, 0.35f, 0f),
            new Vector3(1.19f, 0.34f, 1.24f), red);
        AddPart(parent, "Left Track", PrimitiveType.Cube, new Vector3(-0.55f, 0.21f, 0f),
            new Vector3(0.30f, 0.42f, 1.44f), dark);
        AddPart(parent, "Right Track", PrimitiveType.Cube, new Vector3(0.55f, 0.21f, 0f),
            new Vector3(0.30f, 0.42f, 1.44f), dark);
        AddPart(parent, "Medical Kit", PrimitiveType.Cube, new Vector3(0f, 0.79f, 0f),
            new Vector3(1.06f, 0.63f, 0.94f), red);
        AddPart(parent, "Front Badge", PrimitiveType.Cube, new Vector3(0f, 0.81f, 0.49f),
            new Vector3(0.48f, 0.43f, 0.07f), white);
        AddPart(parent, "Badge Cross Vertical", PrimitiveType.Cube, new Vector3(0f, 0.81f, 0.535f),
            new Vector3(0.095f, 0.33f, 0.023f), red);
        AddPart(parent, "Badge Cross Horizontal", PrimitiveType.Cube, new Vector3(0f, 0.81f, 0.535f),
            new Vector3(0.33f, 0.095f, 0.023f), red);
        AddPart(parent, "Head", PrimitiveType.Cube, new Vector3(0f, 1.32f, 0.02f),
            new Vector3(0.78f, 0.42f, 0.69f), white);
        AddPart(parent, "Visor", PrimitiveType.Cube, new Vector3(0f, 1.29f, 0.38f),
            new Vector3(0.60f, 0.14f, 0.06f), dark);
        AddPart(parent, "Roof Cross Length", PrimitiveType.Cube, new Vector3(0f, 1.54f, 0.02f),
            new Vector3(0.15f, 0.04f, 0.55f), red);
        AddPart(parent, "Roof Cross Width", PrimitiveType.Cube, new Vector3(0f, 1.54f, 0.02f),
            new Vector3(0.55f, 0.04f, 0.15f), red);
        AddPart(parent, "Left Medical Canister", PrimitiveType.Cylinder, new Vector3(-0.62f, 0.75f, -0.24f),
            new Vector3(0.26f, 0.26f, 0.26f), white);
        AddPart(parent, "Right Medical Canister", PrimitiveType.Cylinder, new Vector3(0.62f, 0.75f, -0.24f),
            new Vector3(0.26f, 0.26f, 0.26f), white);
    }

    static void AddPart(
        Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(part.GetComponent<Collider>());
    }

    static LineRenderer AddEffect(Transform parent, string name, Material material, Vector3 position, float width)
    {
        var effect = new GameObject(name);
        effect.transform.SetParent(parent, false);
        effect.transform.localPosition = position;
        var line = effect.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.widthMultiplier = width;
        line.numCapVertices = 4;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
        return line;
    }

    static Material GetOrCreateMaterial(string name, Shader shader, Color color)
    {
        string path = MaterialsPath + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        material = new Material(shader) { color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static void SelectAsset(Object asset)
    {
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }
}
