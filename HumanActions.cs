using UnityEngine;
using UnityEngine.Animations.Rigging;

public class HumanActions : MonoBehaviour
{
    private void Awake()
    {
        entity = GetComponent<baseEntity>();
    }

    [Header("Component References")]
    public baseEntity entity;
    [Header("Actions")]
    public bool isInAction;

    [Header("Rig")]
    public RigBuilder rigBuilder;
    public Rig handRig;
    public Rig weaponRig;

    [Header("Weapon Aim")]
    public MultiAimConstraint weaponAim;

    [Header("Hand IK")]
    public TwoBoneIKConstraint rightHand;
    public TwoBoneIKConstraint leftHand;

    [Header("Animation Points")]
    public Transform rightHandTransform;
    public Transform aim;
    public Transform spine;

    /// <summary>
    /// This is where you call actions if they are unskippable.
    /// </summary>
    /// <param name="animID"></param>
    /// <param name="layer"></param>
    /// <param name="isOverridable"></param>
    /// <param name="isRootMotion"></param>
    public void OnActionCall(string animID, int layer, bool isRootMotion, bool isTrigger)
    {
        isInAction = true;

        if (isRootMotion)
        {
            weaponRig.weight = 0;
            handRig.weight = 0;
        }

        SetWeaponState(false);

        if (isTrigger)
        {
            SetTrigger(animID, isRootMotion);
        }
        else
        {
            ChangeAnimation(animID, layer, isRootMotion);
        }
    }
    public void OnActionEnd()
    {
        entity.entityWeaponManager.entityData.isRaisedWeapon = false;
        entity.entityWeaponManager.entityData.toIdleTimer.x = 0; 
        SetWeaponState(entity.entityWeaponManager.entityData.isRaisedWeapon);
        isInAction = false;
        if (entity.entityAnim.hasRootMotion)
        {
            entity.entityAnim.applyRootMotion = false;
        }
    }

    [Header("Animation")]
    public float animFadeTimer;
    public string currentAnimation;
    public void ChangeAnimation(string animID, int layer, bool isRootMotion)
    {
        entity.entityAnim.applyRootMotion = isRootMotion;
        if (currentAnimation != animID)
        {
            currentAnimation = animID;
            //Debug.Log("Layer : " + layer + " animationID : " + action);
            entity.entityAnim.CrossFade(animID, Time.deltaTime * animFadeTimer, layer);
        }
        else
        {
            //Debug.Log("Same Action, will not trigger!");
        }
    }

    public void SetTrigger(string triggerID, bool isRootMotion)
    {
        entity.entityAnim.applyRootMotion = isRootMotion;
        if (currentAnimation != triggerID)
        {
            currentAnimation = triggerID;
            //Debug.Log("Layer : " + layer + " animationID : " + action);
            entity.entityAnim.SetTrigger(triggerID);
        }
        else
        {
            //Debug.Log("Same Action, will not trigger!");
        }
    }

    /// <summary>
    /// Designed to force animations states non-time dependant, E.I lying dead on ground to reborn for zombies.
    /// </summary>
    /// <param name="action"></param>
    public void ForceAnimation(string animID, int layer, bool isRootMotion)
    {
        //Debug.Log(action);
        if (currentAnimation != animID)
        {
            entity.entityAnim.applyRootMotion = isRootMotion;
            //Debug.Log("We have successfully Forced the action");
            entity.entityAnim.Play(animID, layer);
            currentAnimation = animID;
        }
        else
        {
            //Debug.Log("Same Action, will not Force!");
        }
    }

    #region Weapon Animation
    public void SetHands()
    {
        if(currentWeapon != null)
        {
            rightHand.data.target = currentWeapon.Find("R");
            rightHand.data.hint = currentWeapon.Find("R_Pole");

            leftHand.data.target = currentWeapon.Find("L");
            leftHand.data.hint = currentWeapon.Find("L_Pole");
        }
    }
    public void SetWeapon(Transform weapon, WeaponItem.WeaponType wepType)
    {
        if(weapon != null)
        {
            currentWeapon = weapon;
            weaponAim.data.constrainedObject = currentWeapon;
        }

        SetHands();

        if(wepType == WeaponItem.WeaponType.Melee)
        {
            weaponRig.weight = 0;
            handRig.weight = 0;
        }
        else
        {
            weaponRig.weight = 1;
            handRig.weight = 1;
        }

        rigBuilder.Build();
        entity.entityAnim.Rebind();

        if(entity.entityWeaponManager.currentWeapon == null)
        {
            rigBuilder.enabled = false;
        }
        else
        {
            rigBuilder.enabled = true;
        }
    }

    public Transform currentWeapon;
    public void SetWeaponState(bool isRaised)
    {
        Debug.Log("WeaponState");
        if (entity.entityWeaponManager.currentWeapon)
        {
            if (entity.entityWeaponManager.currentWeapon.weaponType != WeaponItem.WeaponType.Melee)
            {
                if (isRaised)
                {
                    handRig.weight = 1;
                    weaponRig.weight = 1;
                    currentWeapon.transform.SetParent(spine);
                    currentWeapon.transform.localPosition = entity.entityWeaponManager.currentWeapon.weaponAimTransform.position;//wm.currentWeapon.weaponAimTransform.position;
                    currentWeapon.localRotation = Quaternion.Euler(entity.entityWeaponManager.currentWeapon.weaponAimTransform.rotation);//wm.currentWeapon.weaponAimTransform.rotation);
                    Debug.Log("RaisedWeaponState");
                }
                else
                {
                    handRig.weight = 0;
                    weaponRig.weight = 0;
                    currentWeapon.transform.SetParent(rightHandTransform);
                    currentWeapon.transform.localPosition = entity.entityWeaponManager.currentWeapon.weaponLowerTransform.position;//wm.currentWeapon.weaponLowerTransform.position;
                    currentWeapon.localRotation = Quaternion.Euler(entity.entityWeaponManager.currentWeapon.weaponLowerTransform.rotation);//wm.currentWeapon.weaponLowerTransform.rotation);
                    Debug.Log("LoweredWeaponState");
                }
            }
        }
    }

    #endregion
}
