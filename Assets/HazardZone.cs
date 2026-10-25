using UnityEngine;

// RESTRICTION: touching this area kills the ball. Add a Collider2D (e.g. Polygon Collider 2D) + this script.
[RequireComponent(typeof(Collider2D))]
public class HazardZone : MonoBehaviour { }
