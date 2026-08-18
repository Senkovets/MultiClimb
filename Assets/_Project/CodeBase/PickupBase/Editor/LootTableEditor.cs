using UnityEditor;

namespace _Project.CodeBase.PickupBase.Editor
{
    [CustomEditor(typeof(LootTable))]
    public class LootTableEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
 
            LootTable table = (LootTable)target;
 
            if (table.Count == 0)
                return;
 
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Итоговые шансы", EditorStyles.boldLabel);
 
            for (int i = 0; i < table.Count; i++)
            {
                var weapon = table.GetWeapon(i);
                string name = weapon != null ? weapon.DisplayName : "(пусто)";
 
                EditorGUILayout.LabelField(
                    name,
                    $"{table.GetChancePercent(i):F1}%");
            }
        }
    }
}