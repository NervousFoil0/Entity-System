using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;
using Pathfinding.RVO;
public abstract class baseEntity : MonoBehaviour, IHittable<float, Vector3, Color>, IDamagable<bool,float, Transform>, IKillable
{
    #region Core Components
    public baseEntityData entityData;
    public LootSpot entityLootSpot;
    public GameObject entityIcon;
    public AudioSource entitySource;
    public EntityFootsteps footAudio;
    private AIDestinationSetter destinationSetter;
    public Transform player;
    public WeaponManager entityWeaponManager;
    public HumanActions humanActions;

    public Seeker seeker;
    public RVOWayPoint newWayPoint = null;
    private RVOController controller;

    public LayerMask nonWalkable;

    public List<baseEntity> targets = new List<baseEntity>();

    public AttackBox meleeDetection;

    #endregion

    /// <summary>
    /// Returning true means it still has valid target.
    /// </summary>
    /// <returns></returns>
    public bool RefreshTargetList()
    {
        if(targets.Count > 0)
        {
            while (targets[0] == null)
            {
                targets.RemoveAt(0);
                if (targets.Count == 0)
                {
                    return false;
                }
            }
        }
        else
        {
            return false;
        }
        return true;
    }

    //public void SetEntityDestination(Vector3 targetPos)
    //{
    //    ai.destination = targetPos;
    //    ai.SearchPath();
    //    ai.isStopped = false;
    //}

    #region Entity Init
    public void EntityInit()
    {
        footAudio = GetComponent<EntityFootsteps>();
        destinationSetter = GetComponent<AIDestinationSetter>();
        seeker = GetComponent<Seeker>();
        controller = GetComponent<RVOController>();
        entitySource = GetComponent<AudioSource>();
        humanActions = GetComponent<HumanActions>();
        if (humanActions)
        {
            humanActions.entity = this;
        }
        InitEntityRagdoll();
        InitEntityData(entityData);
        InitEntitySensoryData();
        InitWeaponManager();
    }
    public void InitWeaponManager()
    {
        WeaponManager entityWm = transform.root.GetComponent<WeaponManager>();
        if (entityWm)
        {
            entityWeaponManager = entityWm;
        }
    }
    #endregion

    #region Entity Damage
    public bool EntityHit(float damageValue, Vector3 hitPos, Color damageColour)
    {
        IndicatorManager.instance.SpawnDMGIndicator(damageValue, transform, damageColour);
        IndicatorManager.instance.SpawnBloodEffect(hitPos);
        if(entityData.state == baseEntityData.EntityState.dead)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
    public bool EntityTakeDamage(bool isTrueDMG, float damage, Transform dmgSource)
    {
        bool hasTakenDMG = false;

        if (entityData.state != baseEntityData.EntityState.dead)
        {
            if (isTrueDMG)
            {
                Debug.Log("DEALING TRUE DMG add code here.");
            }
            else
            {
                entityData.currentHealth -= damage;
                if(entityData.entityType == baseEntityData.EntityType.Player)
                {
                    MainGUI.instance.UpdateHealth();
                }
            }

            EntityHealthCheck();

            if (entityData.currentHealth <= 0 && entityData.state != baseEntityData.EntityState.dead)
            {
                entityData.state = baseEntityData.EntityState.dead;
                KillEntity();
            }

            hasTakenDMG = true;

            //This is so we can apply different reactions to different entities. Like deer should run when shot.
            if(entityData.state != baseEntityData.EntityState.dead)
            {
                EntityDamageReaction(dmgSource);
            }
        }
        else
        {
            //ai.isStopped = true;
            Debug.Log("Entity is dead");
        }
        return hasTakenDMG;
    }
    public void KillEntity()
    {
        switch (entityData.entityType)
        {
            case baseEntityData.EntityType.standardInfected:
                EntitySpawning.instance.spawnedZombies.Remove(gameObject);
                break;
        }
        entityData.state = baseEntityData.EntityState.dead;
        Destroy(entityIcon);
        Debug.Log("KillEntity" + transform.name);
        ActivateRagdoll(true);
    }
    public void EntityMeleeAttack()
    {
        Debug.Log("EntityAttackTriggered!");
        if (meleeDetection.targetHold != null)
        {
            if(meleeDetection.isPlayerTarget && PlayerMovement.instance.isPaused)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    if (targets[i].transform.tag == "Player")
                    {
                        targets.RemoveAt(i);
                        return;
                    }
                }
            }
            bool isCriticalHit = CriticalDamageCheck();
            float meleeDamage = entityData.meleeAttackDamage;
            if (isCriticalHit)
            {
                meleeDamage *= Random.Range(1.5f, 3f);
            }

            if (meleeDetection.entityDetected.EntityTakeDamage(false, meleeDamage, transform))
            {
                meleeDetection.entityDetected.EntityHit(meleeDamage, meleeDetection.entityDetected.transform.position, TextColourManager.instance.woundedRed);
            }

            //isCriticalDmg = false;

            if (targets[0].DeadCheck())
            {
                Debug.Log("Target Dead!");
                CheckForNewTarget();
            }
            else
            {
                Debug.Log("Target isn't dead!");
            }
        }
    }
    public void EntityMeleeAttackEnd()
    {
        entityData.isAttacking = false;
    }
    public bool CriticalDamageCheck()
    {
        bool isCriticalHit = false;
        float genChance = Random.Range(0f, 100f);
        if(genChance <= entityData.criticalChance)
        {
            isCriticalHit = true;
        }
        return isCriticalHit;
    }
    void EntityHealthCheck()
    {
        if (entityData.currentHealth <= entityData.maxHealth * 0.2f)
        {
            entityData.normalSpeed *= 0.75f;
            entityData.aggroSpeed *= 0.75f;
        }
    }
    void EntityDamageReaction(Transform dmgSource)
    {
        switch (entityData.entityType)
        {
            case baseEntityData.EntityType.standardInfected:

                #region Infected
                if (entityData.state != baseEntityData.EntityState.aggro)
                {
                    entityData.state = baseEntityData.EntityState.aggro;
                    RefreshTargetList();
                    targets.Add(dmgSource.root.GetComponent<baseEntity>());
                }
                #endregion

                break;

            case baseEntityData.EntityType.animal:

                #region Animals
                if (entityData.state != baseEntityData.EntityState.fleeing && entityData.state != baseEntityData.EntityState.aggro)
                {
                    entityData.avoidTarget = dmgSource;
                    entityData.state = baseEntityData.EntityState.fleeing;
                }
                #endregion
                break;

            case baseEntityData.EntityType.Player:

                entityWeaponManager.cameraFunctions.TriggerShake(0.2f, 0.1f);

                break;
        }
    }
    #endregion

    #region Entity Data
    public void InitEntityData(baseEntityData data)
    {
        if (data != null)
        {
            entityData = Instantiate(data);
        }
        else
        {
            entityData = Instantiate(Resources.Load<baseEntityData>("ScriptableObjects/Entities/DefaultData"));
        }

        entityData.aggroSpeed = entityData.normalSpeed * 2;
    }
    public void InitEntitySensoryData()
    {
        sight = GetComponentInChildren<EntitySight>();
        hearing = GetComponentInChildren<EntityHearing>();
        meleeDetection = GetComponentInChildren<AttackBox>();
    }
    #endregion

    #region Entity Animation

    public Animator entityAnim;
    public bool isRunning;

    #region Entity Ragdoll

    public Rigidbody[] entityRBs;
    public Collider[] entityCols;
    public Collider mainCol;
    public Rigidbody mainRB;

    public Transform entityRagdollParent;
    public void InitEntityRagdoll()
    {
        //Debug.Log("Working?" + transform.root.name);
        for (int i = 0; i < transform.childCount; i++)
        {
            if (transform.GetChild(i).tag == "Armature")
            {
                entityRagdollParent = transform.GetChild(i);
                if (i == transform.childCount && entityRagdollParent == null)
                {
                    Debug.LogError("No armature found on child, check tags and script object.");
                }
                break;
            }
        }
        entityCols = entityRagdollParent.GetComponentsInChildren<Collider>();
        entityRBs = entityRagdollParent.GetComponentsInChildren<Rigidbody>();


        mainCol = GetComponent<Collider>();
        mainRB = GetComponent<Rigidbody>();
        entityAnim = GetComponent<Animator>();
        ActivateRagdoll(false);
    }
    public void ActivateRagdoll(bool state)
    {
        //if (ai)
        //{
        //    ai.isStopped = state;
        //}

        entityAnim.enabled = !state;

        mainCol.isTrigger = state; //Not trigger
        mainRB.useGravity = !state; //Use Grav

        foreach (Rigidbody rb in entityRBs)
        {
            rb.useGravity = state;
        }
        foreach (Collider col in entityCols)
        {
            col.isTrigger = !state;
        }
        if (state)
        {
            transform.gameObject.layer = LayerMask.NameToLayer("No Collision");
            Invoke("FreezeRagdoll", 3f);
        }
    }
    public void FreezeRagdoll()
    {
        foreach (Rigidbody rb in entityRBs)
        {
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }
        foreach (Collider col in entityCols)
        {
            col.isTrigger = true;
        }

        if(entityData.entityType == baseEntityData.EntityType.Player)
        {
            OnPlayerDeathLoad();
        }
        else
        {
            CreateLootSpot(entityData.hasLootDrop);
        }
    }
    void OnPlayerDeathLoad()
    {
        baseNPCData playerData = PlayerStatsManager.instance.playerData;
        playerData.cash = 0;
        playerData.currentHealth = playerData.maxHealth * 0.25f;
        playerData.currentHunger = playerData.maxHunger * 0.25f;
        playerData.currentThirst = playerData.maxThirst * 0.25f;

        if (playerData.isGoldMemeber)
        {
            Debug.Log("Is GM!");
            PlayerStatsManager.instance.deathWaitTimer = 0;
            playerData.currentExperience *= 0.5f;
        }
        else
        {
            Debug.Log("Error Triggered?");
            PlayerStatsManager.instance.gainedEXPSinceOutpost *= 0.5f;
            playerData.currentExperience -= PlayerStatsManager.instance.gainedEXPSinceOutpost;
            if (playerData.currentExperience < 0)
            {
                playerData.currentExperience = 0;
            }
            PlayerStatsManager.instance.deathWaitTimer = 120;
            //Death Wait Timer = 120s;
        }

        if (playerData.currentExperience < playerData.experienceCap)
        {
            playerData.levelUp = false;
        }

        if (InventoryManager.instance.inventoryGrids[5].inventoryItems.Count > 0)
        {
            int destroyItemIndex = Random.Range(0, InventoryManager.instance.inventoryGrids[5].inventoryItems.Count);
            if (InventoryManager.instance.inventoryGrids[5].inventoryItems[destroyItemIndex] != null)
            {
                InventoryManager.instance.inventoryGrids[5].inventoryItems[destroyItemIndex].DestroyInventoryItem();
            }
        }

        Scene_Manager.instance.SwitchToLoad("Outpost");
    }

    #endregion

    #endregion

    #region Entity Loot Generation
    void CreateLootSpot(bool hasCustomLoot)
    {
        if (hasCustomLoot)
        {
            //Debug.Log("Custom entity loot drop");
            SphereCollider spotCol = gameObject.AddComponent<SphereCollider>();
            spotCol.radius = 1.5f;
            spotCol.isTrigger = true;
            entityLootSpot = gameObject.AddComponent<LootSpot>();
            Invoke("DelayLoadCustomTable", Time.deltaTime);
        }
        else
        {
            if (!entityData.stopLootDrop)
            {
                float chance = Random.Range(1f, 100f);
                //Debug.Log("Chance is : " + chance + " Loot chance : " + entityData.lootChance);
                if (chance < entityData.lootChance)
                {
                    SphereCollider spotCol = gameObject.AddComponent<SphereCollider>();
                    spotCol.radius = 1.5f;
                    spotCol.isTrigger = true;
                    entityLootSpot = gameObject.AddComponent<LootSpot>();
                    //Debug.Log("Custom entity doesnt have loot so random gen");
                    LootingManager.instance.LootRequest(null, entityLootSpot.data);
                }
            }
        }
        if(entityLootSpot == null)
        {
            Destroy(gameObject, 5f);
        }

        gameObject.layer = 21;
    }
    void DelayLoadCustomTable()
    {
        StartCoroutine(LoadCustomTable());
    }
    IEnumerator LoadCustomTable()
    {
        entityLootSpot.data.searched = true;
        if (entityData.hasCustomLoot)
        {
            for (int i = 0; i < entityLootSpot.data.numberOfItems; i++)
            {
                int pickedIndex = Random.Range(0, entityData.lootDrop.Length);
                entityLootSpot.data.lootableItems.Add(Instantiate(entityData.lootDrop[pickedIndex]));
                InventoryManager.instance.inventoryGrids[7].SpawnItem(entityLootSpot.data.lootableItems[entityLootSpot.data.lootableItems.Count - 1]);
                yield return new WaitForSeconds(0.35f);
            }
        }
        else
        {

        }
        InventoryManager.instance.inventoryGrids[7].ClearItems();
    }

    #endregion

    #region Entity Sensory

    public EntitySight sight;
    public EntityHearing hearing;

    public bool DeadCheck()
    {
        if(entityData.currentHealth <= 0)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
    public baseEntity CheckForNewTarget()
    {
        if (RefreshTargetList())
        {
            int closestIndex = 0;
            float closestDis = Vector3.Distance(transform.position, targets[0].transform.position);

            for (int i = 0; i < targets.Count; i++)
            {
                float curDis = Vector3.Distance(transform.position, targets[i].transform.position);
                if (curDis < closestDis)
                {
                    closestDis = curDis;
                    closestIndex = i;
                }
            }
            return targets[closestIndex];
        }
        else
        {
            entityData.state = baseEntityData.EntityState.aggro;
            return null;
        }
    }

    #endregion

    #region Entity AI Functions

    [Header("Entity AI")]
    public float repathRate = 1;

    public float nextRepath = 0;

    public Vector3 target;
    public bool canSearchAgain = true;
    public float currentSpeed;
    public float maxSpeed = 10;

    public Path path = null;

    public List<Vector3> vectorPath;
    int wp;

    public float moveNextDist = 1;
    public float slowdownDistance = 1;
    public LayerMask groundMask;

    public bool hasWanderPath;
    /// <summary>Set the point to move to</summary>
    public void SetTarget(Vector3 targetPos)
    {
        target = targetPos;
        RecalculatePath();
    }
    public void RecalculatePath()
    {
        canSearchAgain = false;
        nextRepath = Time.time + repathRate * (Random.value + 0.5f);
        seeker.StartPath(transform.position, target, OnPathComplete);
    }
    public void OnPathComplete(Path _p)
    {
        ABPath p = _p as ABPath;

        canSearchAgain = true;

        if (path != null) path.Release(this);
        path = p;
        p.Claim(this);

        if (p.error)
        {
            wp = 0;
            vectorPath = null;
            return;
        }


        Vector3 p1 = p.originalStartPoint;
        Vector3 p2 = transform.position;
        p1.y = p2.y;
        float d = (p2 - p1).magnitude;
        wp = 0;

        vectorPath = p.vectorPath;
        Vector3 waypoint;

        if (moveNextDist > 0)
        {
            for (float t = 0; t <= d; t += moveNextDist * 0.6f)
            {
                wp--;
                Vector3 pos = p1 + (p2 - p1) * t;

                do
                {
                    wp++;
                    waypoint = vectorPath[wp];
                } while (controller.To2D(pos - waypoint).sqrMagnitude < moveNextDist * moveNextDist && wp != vectorPath.Count - 1);
            }
        }
    }
    public void MoveToTarget()
    {
        Vector3 pos = transform.position;
        newWayPoint = CheckWayPointDistance();
        if (vectorPath != null && vectorPath.Count != 0)
        {
            var rvoTarget = (newWayPoint.wayPoint - pos).normalized * newWayPoint.remainingDistance + pos;
            var desiredSpeed = Mathf.Clamp01(newWayPoint.remainingDistance / slowdownDistance) * currentSpeed;;
            Debug.DrawLine(transform.position, newWayPoint.wayPoint, Color.red);
            controller.SetTarget(rvoTarget, desiredSpeed, currentSpeed);

            switch (entityData.state)
            {
                case baseEntityData.EntityState.aggro:
                    if (newWayPoint.remainingDistance <= entityData.meleeAttackRadius)
                    {
                        entityData.state = baseEntityData.EntityState.attacking;
                    }
                    break;
                case baseEntityData.EntityState.wandering:
                    if(newWayPoint.remainingDistance <= 0.5f)
                    {
                        hasWanderPath = false;
                    }
                    break;
            }
        }
        else
        {
            // Stand still
            Debug.Log("StandStill");
            controller.SetTarget(pos, currentSpeed, currentSpeed);//currentSpeed, maxSpeed);
        }

        var movementDelta = controller.CalculateMovementDelta(Time.deltaTime);
        pos += movementDelta;

        if (Time.deltaTime > 0 && movementDelta.magnitude / Time.deltaTime > 0.01f)
        {
            var rot = transform.rotation;
            var targetRot = Quaternion.LookRotation(movementDelta, controller.To3D(Vector2.zero, 1));
            const float RotationSpeed = 5;
            if (controller.movementPlane == MovementPlane.XY)
            {
                targetRot = targetRot * Quaternion.Euler(-90, 180, 0);
            }
            transform.rotation = Quaternion.Slerp(rot, targetRot, Time.deltaTime * RotationSpeed);
        }

        if (controller.movementPlane == MovementPlane.XZ)
        {
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up, Vector3.down, out hit, 2, groundMask))
            {
                pos.y = hit.point.y;
            }
        }

        transform.position = pos;
    }
    public RVOWayPoint CheckWayPointDistance()
    {
        RVOWayPoint newWayPoint = new RVOWayPoint();
        Vector3 pos = transform.position;

        if (vectorPath != null && vectorPath.Count != 0)
        {
            while ((controller.To2D(pos - vectorPath[wp]).sqrMagnitude < moveNextDist * moveNextDist && wp != vectorPath.Count - 1) || wp == 0)
            {
                wp++;
            }
            var p1 = vectorPath[wp - 1];
            var p2 = vectorPath[wp];
            var t = VectorMath.LineCircleIntersectionFactor(controller.To2D(transform.position), controller.To2D(p1), controller.To2D(p2), moveNextDist);
            t = Mathf.Clamp01(t);
            Vector3 waypoint = Vector3.Lerp(p1, p2, t);
            newWayPoint.wayPoint = waypoint;
            float remainingDistance = controller.To2D(waypoint - pos).magnitude + controller.To2D(waypoint - p2).magnitude;
            for (int i = wp; i < vectorPath.Count - 1; i++) remainingDistance += controller.To2D(vectorPath[i + 1] - vectorPath[i]).magnitude;
            newWayPoint.remainingDistance = remainingDistance;
            return newWayPoint;
        }
        else
        {
            return null;
        }
    }

    public Vector2 radius = new Vector2(-15, 15);
    public Vector3 PickRandomPoint()
    {
        bool pointPicked = false;
        Vector3 point = new Vector3(1, 1, 1);
        while (!pointPicked)
        {
            Vector3 offSet = new Vector3(Random.value - 0.5f, 0, Random.value - 0.5f).normalized * Random.Range(radius.x, radius.y);
            Vector3 wanderPoint = new Vector3(transform.position.x, 15, transform.position.z) + offSet;
            RaycastHit hit;
            if (Physics.Raycast(wanderPoint, -transform.up, out hit, Mathf.Infinity, nonWalkable))
            {
                Debug.DrawRay(wanderPoint, (-transform.up * 25), Color.red, 5f);
            }
            else
            {
                Debug.DrawRay(wanderPoint, (-transform.up * 25), Color.blue, 5f);
                point = new Vector3(wanderPoint.x, Terrain.activeTerrain.SampleHeight(hit.point) + Terrain.activeTerrain.transform.position.y, wanderPoint.z);
                pointPicked = true;
            }
        }
        return point;
    }

    #endregion
}

[System.Serializable]
public class RVOWayPoint
{
    public Vector3 wayPoint;
    public float remainingDistance;
}
