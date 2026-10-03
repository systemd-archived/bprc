using System.Collections.Generic;
using UnityEngine;

namespace BadPiggies.Multiplayer
{
/// <summary>
/// Lays the three main-menu start buttons out as a single row: Cake Race, Play, then Multiplayer.
///
/// The menu authored Cake Race and Play side by side and the injected Multiplayer button used to be
/// dropped at a fixed x of 7, which put it on top of Play on one side and of the CrossPromo button on
/// the other. This helper measures what each button actually occupies, shrinks the whole row until it
/// fits, centres the row on Play - the middle button and the menu's primary action - and moves the
/// CrossPromo button aside when centring would otherwise run the row into it.
///
/// Every button is moved into a slot (an empty parent) that carries the row's scale. That matters for
/// Play and for the cloned Multiplayer button: both carry ButtonAnimation, which resets its own
/// transform.localScale to one whenever it is enabled, so scaling the button itself would not stick.
/// </summary>
public static class MultiplayerMenuLayout
{
    public const string CakeRaceButtonName = "CakeRaceButton";

    public const string CrossPromoName = "CrossPromoMainMenu";

    public const string SlotPrefix = "MultiplayerRowSlot_";

    /// <summary>World units between two neighbouring buttons.</summary>
    public const float Gap = 0.9f;

    /// <summary>The row is shrunk (never grown) until it is this wide, so three buttons still fit.</summary>
    public const float TargetRowWidth = 17.5f;

    public const float MinimumScale = 0.5f;

    /// <summary>Right edge the row prefers when nothing else is in the way.</summary>
    public const float RowRightEdge = 6.8f;

    /// <summary>Clearance kept between the row and the CrossPromo button.</summary>
    public const float CrossPromoMargin = 0.25f;

    /// <summary>
    /// Arranges cake, play and multiplayer from left to right. Missing entries are skipped, so the row still
    /// looks right when Cake Race is not in the menu.
    /// </summary>
    public static void Arrange(GameObject cake, GameObject play, GameObject multiplayer)
    {
        List<GameObject> buttons = new List<GameObject>();
        if (cake != null && cake.activeInHierarchy)
        {
            buttons.Add(cake);
        }
        if (play != null && play.activeInHierarchy)
        {
            buttons.Add(play);
        }
        if (multiplayer != null && multiplayer.activeInHierarchy)
        {
            buttons.Add(multiplayer);
        }
        if (buttons.Count == 0)
        {
            return;
        }
        // Play decides where the row lives: Cake Race can have been detached by its unlock sequence.
        Transform rowParent = play != null && play.transform.parent != null
            ? play.transform.parent
            : buttons[0].transform.parent;

        // 1) slot every button so scaling the row cannot fight the buttons' own animations.
        List<Transform> slots = new List<Transform>();
        foreach (GameObject button in buttons)
        {
            slots.Add(EnsureSlot(button, rowParent));
        }

        // 2) shrink the row until it fits the width budget.
        float natural = Gap * (buttons.Count - 1);
        foreach (GameObject button in buttons)
        {
            natural += Footprint(button).size.x;
        }
        float scale = natural > 0.0001f ? Mathf.Clamp(TargetRowWidth / natural, MinimumScale, 1f) : 1f;
        foreach (Transform slot in slots)
        {
            slot.localScale = Vector3.one * scale;
        }

        // 3) measure again (the buttons' own colliders now carry the row scale) and place them.
        float total = Gap * (buttons.Count - 1);
        for (int i = 0; i < buttons.Count; i++)
        {
            total += Footprint(buttons[i]).size.x;
        }
        float rowCenterY = Footprint(play != null ? play : buttons[0]).center.y;
        float right = RightBound();
        float cursor = right - total;
        for (int i = 0; i < buttons.Count; i++)
        {
            GameObject button = buttons[i];
            Bounds footprint = Footprint(button);
            float centerX = cursor + footprint.size.x * 0.5f;
            Vector3 delta = new Vector3(centerX - footprint.center.x, rowCenterY - footprint.center.y, 0f);
            slots[i].position += delta;
            cursor += footprint.size.x + Gap;
        }

        // 4) The row is centred on the Play button. Play is the middle button and the menu's primary
        //    action, so the row is slid sideways until Play's centre lands on the middle of the screen
        //    rather than wherever the right-aligned packing above happens to leave it.
        GameObject anchor = play != null ? play : buttons[buttons.Count / 2];
        float shift = 0f - Footprint(anchor).center.x;
        if (Mathf.Abs(shift) > 0.0001f)
        {
            foreach (Transform slot in slots)
            {
                slot.position += new Vector3(shift, 0f, 0f);
            }
        }

        // 5) Centring can now push the row into the CrossPromo button. That button is not part of the
        //    row, so it is the one that gives way - but only when the two really share screen space.
        KeepCrossPromoClear(buttons);

        Debug.Log(string.Format(
            "[Multiplayer] Main menu row ({0} buttons, scale {1:0.###}, gap {2:0.##}): {3}",
            buttons.Count, scale, Gap, Describe(buttons)));
    }

    /// <summary>Right edge the row may reach: the CrossPromo button is kept clear when it is present.</summary>
    public static float RightBound()
    {
        float right = RowRightEdge;
        GameObject promo = Find("CrossPromoMainMenu");
        if (promo != null && promo.activeInHierarchy)
        {
            Bounds bounds = Footprint(promo);
            if (bounds.size.x > 0.0001f)
            {
                right = Mathf.Min(right, bounds.min.x - CrossPromoMargin);
            }
        }
        return right;
    }

    /// <summary>
    /// Slides the CrossPromo button just far enough right that it stops sharing pixels with the row.
    /// </summary>
    /// <remarks>
    /// The test has to be two-dimensional. A horizontal-only comparison would move the promo even when
    /// it sits on a different line of the menu, where overlapping x ranges are perfectly fine, and it
    /// would leave a real overlap unfixed whenever the two happen to be at the same height. Both axes
    /// are therefore compared before anything is moved.
    /// </remarks>
    private static void KeepCrossPromoClear(List<GameObject> buttons)
    {
        GameObject promo = Find(CrossPromoName);
        if (promo == null || !promo.activeInHierarchy)
        {
            return;
        }
        Bounds promoBounds = Footprint(promo);
        if (promoBounds.size.x <= 0.0001f || promoBounds.size.y <= 0.0001f)
        {
            return;
        }
        Bounds rowBounds = Footprint(buttons[0]);
        for (int i = 1; i < buttons.Count; i++)
        {
            rowBounds.Encapsulate(Footprint(buttons[i]));
        }
        bool sharesRow = rowBounds.min.y < promoBounds.max.y && promoBounds.min.y < rowBounds.max.y;
        if (!sharesRow)
        {
            return;
        }
        float deficit = rowBounds.max.x + CrossPromoMargin - promoBounds.min.x;
        if (deficit <= 0f)
        {
            return;
        }
        promo.transform.position += new Vector3(deficit, 0f, 0f);
        Debug.Log(string.Format(
            "[Multiplayer] CrossPromo button moved {0:0.###} right so the centred row keeps clear of it.", deficit));
    }

    /// <summary>Union of every collider under the object, falling back to its renderers.</summary>
    public static Bounds Footprint(GameObject go)
    {
        bool has = false;
        Bounds bounds = new Bounds(go.transform.position, Vector3.zero);
        Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            if (collider.bounds.size.sqrMagnitude <= 0f)
            {
                continue;
            }
            if (!has)
            {
                bounds = collider.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }
        if (has)
        {
            return bounds;
        }
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer.bounds.size.sqrMagnitude <= 0f)
            {
                continue;
            }
            if (!has)
            {
                bounds = renderer.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return bounds;
    }

    private static Transform EnsureSlot(GameObject button, Transform rowParent)
    {
        string slotName = SlotPrefix + button.name;
        Transform slot = rowParent != null ? rowParent.Find(slotName) : null;
        if (slot != null)
        {
            if (button.transform.parent != slot)
            {
                button.transform.SetParent(slot, true);
            }
            return slot;
        }
        GameObject holder = new GameObject(slotName);
        holder.layer = button.layer;
        slot = holder.transform;
        slot.SetParent(rowParent, false);
        slot.localPosition = button.transform.localPosition;
        slot.localRotation = button.transform.localRotation;
        slot.localScale = Vector3.one;
        button.transform.SetParent(slot, true);
        return slot;
    }

    private static string Describe(List<GameObject> buttons)
    {
        List<string> parts = new List<string>();
        foreach (GameObject button in buttons)
        {
            Bounds bounds = Footprint(button);
            parts.Add(string.Format("{0} {1:0.##} wide at x {2:0.##} (y {3:0.##})",
                button.name, bounds.size.x, bounds.center.x, bounds.center.y));
        }
        return string.Join(", ", parts.ToArray());
    }

    private static GameObject Find(string name)
    {
        foreach (GameObject go in Object.FindObjectsOfType<GameObject>())
        {
            if (go.name == name && go.scene.IsValid() && go.scene.isLoaded)
            {
                return go;
            }
        }
        return null;
    }
}
}
