﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;
using UnityEngine.SocialPlatforms;

public class Player : Character
{
    public bool gettingMove;
    public bool gettingAction;
    public bool gettingTarget;
    public bool gettingItem;
    public bool looting;
    private MenuManager menuManager;
    private MouseManager mouseManager;

    // Start is called before the first frame update
    protected override void Start()
    {
        base.Start();

        gettingMove = false;
        gettingTarget = false;
        looting = false;
        menuManager = GameObject.Find("MenuManager").GetComponent<MenuManager>();
        mouseManager = GameObject.Find("MouseManager").GetComponent<MouseManager>();

        GameManager.instance.AddCharacterToList(this);
    }

    // Update is called once per frame
    void Update()
    {
        if (GameManager.instance.dialogueInProgress)
            return;

        if (gettingMove)
        {
            gettingMove = mouseManager.GetMoveInput(this, paths);
            if (!gettingMove)
            {
                StartCoroutine(SelectedPath());
            }
        }
        if (gettingTarget)
        {
            gettingTarget = mouseManager.GetTargetInput(this, GetObjects());
            if (!gettingTarget)
            {
                Act();
            }
        }
    }

    protected override IEnumerator NextStep()
    {
        if (!playable) {
            yield return base.NextStep();
        }

        // Prevents the rest of the players turn from happening until dialogue is resolved (hopefully)
        if (GameManager.instance.dialogueInProgress)
            yield return new WaitUntil(() => GameManager.instance.dialogueInProgress == false);

        // yield return new WaitForSeconds(actionDelay);
        GameManager.instance.CameraTarget(this.gameObject);
        //Debug.Log("Moves: " + movesLeft + ", Actions: " + actionsLeft);
        if ((movesLeft != totalMoves || actionsLeft != totalActions) && lastState.moves == movesLeft && lastState.actions == actionsLeft && lastState.position == (Vector2)transform.position)
            menuManager.HideBackButton();
        else
            menuManager.ShowBackButton();

        UpdateObjectives();
        GetPaths();
        yield return new WaitForSeconds(moveTime);
        FindPath();
    }

    protected override void UpdateObjectives()
    {
        if (!playable) {
            base.UpdateObjectives();
            return;
        }

        currentObjective = new Objective(null, null);
    }

    protected void GetPaths()
    {
        paths.Clear();
        GetPaths(transform.position, new Vector2[0], movesLeft);
    }

    protected void GetPaths(Vector2 next, Vector2[] path, int remainingMoves) //update with better alg/queue?
    {
        if (Array.Exists(path, element => element == next)) { return; }
        Vector2 previous = ((path.Length == 0) ? (Vector2)transform.position : path[path.Length - 1]);
        boxCollider.enabled = false;
        RaycastHit2D[] hits = Physics2D.LinecastAll(previous, next, Collisions);
        boxCollider.enabled = true;
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.transform != null)
            {
                if (hit.transform.gameObject.tag != "Roof" && hit.transform.gameObject.tag != "Damaging" && hit.transform.gameObject.tag != "Chair" && (hit.transform.gameObject.tag != "Door" || !hit.transform.gameObject.GetComponent<Door>().IsOpen()))
                    return;
            }
        }

        Vector2[] newPath = new Vector2[path.Length + 1];
        Array.Copy(path, newPath, path.Length);
        newPath[newPath.Length - 1] = next;
        if (!paths.ContainsKey(next)) { paths.Add(next, newPath); }
        else
        {
            paths.TryGetValue(next, out path);
            if (newPath.Length < path.Length)
            {
                paths.Remove(next);
                paths.Add(next, newPath);
            }
            else
            {
                return;
            }
        }

        remainingMoves--;
        if (remainingMoves >= 0)
        {
            GetPaths(next + new Vector2(1, 0), newPath, remainingMoves);
            GetPaths(next + new Vector2(-1, 0), newPath, remainingMoves);
            GetPaths(next + new Vector2(0, 1), newPath, remainingMoves);
            GetPaths(next + new Vector2(0, -1), newPath, remainingMoves);
        }
    }

    protected override void FindPath()
    {
        if (!playable) {
            base.FindPath();
            return;
        }

        if (paths.Count == 1)
        {
            paths.TryGetValue(transform.position, out pathToObjective);
            StartCoroutine(SelectedPath());
        }
        else
        {
            menuManager.ShowPaths(paths);
            gettingMove = true;
            Debug.Log("Player waiting for move input");
        }
    }

    protected IEnumerator SelectedPath()
    {
        if (pathToObjective.Length > 1)
        {
            pathToObjective = pathToObjective.Skip(1).ToArray();
            yield return StartCoroutine(FollowPath());
            StartCoroutine(NextStep());
        }
        else
        {
            GetAvailableTargets();
            GetAvailableActions();
            SelectAction();
        }
    }

    protected void SelectAction()
    {
        if (actions.Count <= 1 && lastState.actions == 0 && lastState.moves == 0)
            StartCoroutine(EndTurn());
        else
        {
            menuManager.SetupActionMenu(actions);
            menuManager.ShowBackButton();
            gettingAction = true;
        }
    }

    public void GetActionInput(string action)
    {
        menuManager.HideActionMenu();
        if (currentObjective == null) { //temp due to weird error
            Debug.Log("Objective Null!");
            currentObjective = new Objective(null, null);
        }
        currentObjective.action = action;
        gettingAction = false;
        if (currentObjective.action != "Wait")
            SelectTarget();
        else
            Act();
    }

    protected void SelectTarget()
    {
        menuManager.ShowObjects(GetObjects());
        gettingTarget = true;
        Debug.Log("Player waiting for target input");
    }

    private List<InteractableObject> GetObjects()
    {
        List<InteractableObject> objects;
        actableObjects.TryGetValue(currentObjective.action, out objects);
        return objects;
    }

    protected override void SelectItem(String type)
    {
        if (!playable) {
            base.SelectItem(type);
            return;
        }

        menuManager.ShowInventory(type, inventory, type == "Weapon" ? GetDistance(currentObjective.target) : 0);
        gettingItem = true;
        Debug.Log("Player waiting for item input");
    }

    public override void ChooseItem(HoldableObject item)
    {
        if (looting)
        {
            Storage storage = currentObjective.target.gameObject.GetComponent<Storage>();
            if (storage != null)
            {
                storage.Remove(item);
                menuManager.ShowInventory("", inventory, 0, storage.items);
            }
            else
            {
                weightStolen += item.weight;
                Character character = currentObjective.target.gameObject.GetComponent<Character>();
                if (CaughtStealing(character))
                {
                    character.Enemy(this);
                    Back();
                    return;
                }
                character.Remove(item);
                menuManager.ShowInventory("", inventory, 0, character.inventory);
            }
            Pickup(item);
            return;
        }
        
        gettingItem = false;
        menuManager.HideInventory();
        base.ChooseItem(item);
    }

    protected override void Loot(InteractableObject toLoot)
    {
        looting = true;
        GameManager.instance.CameraTarget(toLoot.gameObject);
        Storage storage = toLoot.gameObject.GetComponent<Storage>();
        storage.Open();
        menuManager.ShowInventory("", inventory, 0, storage.items);
    }

    protected override void Steal(InteractableObject toStealFrom)
    {
        looting = true;
        this.weightStolen = 0;
        GameManager.instance.CameraTarget(toStealFrom.gameObject);
        Character character = toStealFrom.gameObject.GetComponent<Character>();
        menuManager.ShowInventory("", inventory, 0, character.inventory);
    }

    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (other.tag == "Exit")
        {
            rationale += 3;
            Invoke("Restart", 1f);
            enabled = false;
        }

        base.OnTriggerEnter2D(other);
    }

    private void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void CheckIfGameOver()
    {
        if (health <= 0)
        {
            GameManager.instance.GameOver();
        }
    }

    public override void TakeDamage (double loss)
    {
        base.TakeDamage(loss);
        menuManager.UpdateHealth(health);
        animator.SetTrigger("playerHit");
        CheckIfGameOver();
    }

    public override void Heal (double amount)
    {
        base.Heal(amount);
        menuManager.UpdateHealth(health);
    }

    protected override void TalkTo(InteractableObject toTalkTo)
    {
        menuManager.HideBackButton();
        base.TalkTo(toTalkTo);
    }

    public void Back()
    {
        if (looting)
        {
            looting = false;
            Storage storage = currentObjective.target.gameObject.GetComponent<Storage>();
            if (storage != null)
                storage.Close();
            menuManager.HideInventory();
            menuManager.HideBackButton();
            StartCoroutine(NextStep());
            return;
        }
        
        if (gettingMove)
        {
            if (movesLeft == totalMoves && actionsLeft == totalActions && lastState.moves == movesLeft && lastState.actions == actionsLeft && lastState.position == (Vector2)transform.position)
            {
                menuManager.HideIndicators();
                menuManager.HideBackButton();
                isTurn = false;
                gettingMove = false;
                StartCoroutine(GameManager.instance.NextTurn());
                return;
            }
            else
            {
                menuManager.HideIndicators();
                gettingMove = false;
            }
        }
        else if (gettingAction)
        {
            menuManager.HideActionMenu();
            gettingAction = false;
        }
        else if (gettingTarget)
        {
            menuManager.HideIndicators();
            gettingTarget = false;
        }
        else if (gettingItem)
        {
            menuManager.HideInventory();
            gettingItem = false;
        }

        health = lastState.health;
        menuManager.UpdateHealth(health);
        movesLeft = lastState.moves;
        actionsLeft = lastState.actions;
        transform.position = lastState.position;
        StartCoroutine(NextStep());
    }

    protected override IEnumerator EndTurn()
    {
        menuManager.HideBackButton();
        yield return StartCoroutine(base.EndTurn());
    }
}
