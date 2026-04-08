using UnityEngine;

/// <summary>
/// A food pellet sitting in the world. Creatures query the FoodSpawner to
/// find the nearest food; when eaten this object returns itself to the pool.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Food : MonoBehaviour
{
    public enum FoodType { Plant, Meat }

    public FoodType foodType   { get; private set; }
    public float    nutritionValue { get; private set; }

    private SpriteRenderer sr;

    // Colors
    static readonly Color PlantColor = new (0.35f, 0.78f, 0.25f, 1f);
    static readonly Color MeatColor  = new (0.85f, 0.28f, 0.28f, 1f);

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.sortingOrder = 0;
    }

    /// <summary>Initialise or re-initialise after pool retrieval.</summary>
    public void Initialise(Vector2 position, FoodType type, float nutrition)
    {
        transform.position = new (position.x, position.y, 0f);
        foodType       = type;
        nutritionValue = nutrition;

        float scale = Mathf.Lerp(0.15f, 0.35f, nutrition); // size reflects value
        transform.localScale = Vector3.one * scale;

        sr.color = type == FoodType.Plant ? PlantColor : MeatColor;
        gameObject.SetActive(true);
    }

    /// <summary>Called by a creature when it eats this pellet.</summary>
    public void ConsumedBy(Creature creature)
    {
        FoodSpawner.Instance.ReturnToPool(this);
    }
}