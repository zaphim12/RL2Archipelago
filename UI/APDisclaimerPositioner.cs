using System.Collections;
using UnityEngine;

namespace RL2Archipelago.UI;

/// <summary>
/// Stand-in for the game's <c>DisclaimerPositionController</c>. The vanilla controller
/// reparents the disclaimer onto a shared "slot" GameObject and snaps to its world
/// position. We can't reuse that directly: the slot lives under a sibling suboption
/// panel that Unity deactivates while our panel is shown, so reparenting onto it makes
/// our disclaimer inactive (invisible) and throws "Coroutine couldn't be started …".
///
/// This component does the position-copy half only, exactly what the vanilla controller
/// does on lines 23/34, using the slot's hierarchy-independent world position, while
/// leaving the disclaimer parented inside our always-active panel.
/// </summary>
public class APDisclaimerPositioner : MonoBehaviour
{
    // The vanilla scene "slot" placed at the bottom-of-menu disclaimer spot.
    // World position is valid even though the slot's own subtree is inactive.
    public GameObject Slot { get; set; }

    private void OnEnable()
    {
        SnapToSlot();
        // Mirror the vanilla controller's one-frame-late correction, which exists so the
        // position is right after the layout settles on the frame the panel opens.
        if (isActiveAndEnabled) StartCoroutine(SnapNextFrame());
    }

    private IEnumerator SnapNextFrame()
    {
        yield return null;
        SnapToSlot();
    }

    private void SnapToSlot()
    {
        if (Slot != null)
            transform.position = Slot.transform.position;
    }
}
