using UnityEngine;

/// <summary>
/// Посадка всадника: после анимации (LateUpdate) сгибает ноги героя по бокам коня и вытягивает руки
/// к поводьям. Аниматор каждый кадр заново ставит позу «стоя», а мы поверх неё поворачиваем кости.
/// </summary>
public class RiderPose : MonoBehaviour
{
    public Animator animator;
    public Transform horse;
    public float thighForward = 72f, thighSpread = 24f, shinBack = 78f, armForward = 42f, forearm = 38f;

    void LateUpdate()
    {
        if (animator == null || horse == null) return;
        Vector3 right = horse.right, fwd = horse.forward;
        Leg(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, -1f, right, fwd);
        Leg(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, 1f, right, fwd);
        Arm(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, right);
        Arm(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, right);
    }

    void Leg(HumanBodyBones up, HumanBodyBones low, float side, Vector3 right, Vector3 fwd)
    {
        Transform u = animator.GetBoneTransform(up), l = animator.GetBoneTransform(low);
        if (u == null || l == null) return;
        u.rotation = Quaternion.AngleAxis(-thighForward, right) * Quaternion.AngleAxis(thighSpread * side, fwd) * u.rotation;
        l.rotation = Quaternion.AngleAxis(shinBack, right) * l.rotation;
    }

    void Arm(HumanBodyBones up, HumanBodyBones low, Vector3 right)
    {
        Transform u = animator.GetBoneTransform(up), l = animator.GetBoneTransform(low);
        if (u == null || l == null) return;
        u.rotation = Quaternion.AngleAxis(-armForward, right) * u.rotation;
        l.rotation = Quaternion.AngleAxis(-forearm, right) * l.rotation;
    }
}
