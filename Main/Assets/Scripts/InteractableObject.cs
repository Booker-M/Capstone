﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractableObject : MonoBehaviour
{
    public LayerMask Collisions;
    public double maxHealth;
    public bool damageable;
    public bool repairable;
    public bool leavesCorpse; // Corpse refers to inanimate objects as well- a destroyed lever is a "corpse"
    public bool walkOver;
    public Sprite damagedSprite;
    public Sprite corpseSprite;
    public GameObject fire;

    protected SpriteRenderer spriteRenderer;
    protected double health;
    protected bool isCorpse;
    protected SortedSet<String> receiveActions;
    protected BoxCollider2D boxCollider;
    protected Rigidbody2D rb2D;

    // Start is called before the first frame update
    protected virtual void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        boxCollider = GetComponent<BoxCollider2D>();
        rb2D = GetComponent<Rigidbody2D>();
        health = maxHealth;
        //leavesCorpse = false;
        isCorpse = false;
        receiveActions = new SortedSet<String>();

        UpdatePosition();
    }

    /**
     * Triggered when an NPC / event should damage the object.
     * Note- If need be, you could make a "takeTrueDamage" function to damage "undamageable" objects
     * 
     * @param damage How much health the action takes away
     */
    public virtual IEnumerator TakeDamage(double damage)
    {
        if (!damageable) yield break;
        if (damagedSprite != null)
            spriteRenderer.sprite = damagedSprite;
        int oldHealth = (int) health;
        health = Math.Max(health - damage, 0);
        SoundManager.instance.TakeDamage();
        yield return StartCoroutine(UpdateHealthBar(oldHealth));

        if (health <= 0)
        {
            if (fire != null)
            {
                Instantiate(fire, transform.position, Quaternion.identity);
            }
            StartCoroutine(animateDeath());
            GameManager.instance.RemoveDeadCharacters(); // For some reason, this didn't work, so instead, GameManager just doesn't move characters at <= 0 health
            ErasePosition();
            if (leavesCorpse)
            {
                // spriteRenderer.sprite = corpseSprite;
                isCorpse = true;
            } else {
                gameObject.SetActive(false);
            }
        }
        else
        {
            UpdatePosition();
        }

        Debug.Log(this + " took " + damage + " damage");
    }

    protected virtual IEnumerator animateDeath() {
        gameObject.SetActive(false);
        yield return null;
    }

    public virtual IEnumerator Heal(double amount)
    {
        if (!damageable) yield break;
        int oldHealth = (int) health;
        health = Math.Min(health + amount, maxHealth);
        SoundManager.instance.Heal();
        yield return StartCoroutine(UpdateHealthBar(oldHealth));
        UpdatePosition();
    }

    public virtual double GetHealth()
    {
        return health;
    }

    public virtual bool IsDamaged()
    {
        return health < maxHealth;
    }

    public virtual SortedSet<String> GetActions()
    {
        receiveActions.Clear();

        if (damageable)
            receiveActions.Add("Attack");

        if (repairable && IsDamaged())
            receiveActions.Add("Heal");

        return receiveActions;
    }

    public virtual int GetDistance(InteractableObject o)
    {
        return (int)(Math.Abs(o.transform.position.x - transform.position.x) + Math.Abs(o.transform.position.y - transform.position.y));
    }

    protected virtual void ErasePosition()
    {
        GameManager.instance.UpdateNode(transform.position, true, 0);
    }

    protected virtual void UpdatePosition()
    {
        GameManager.instance.UpdateNode(transform.position, damageable, walkOver ? 0 : (float)health);
    }

    protected IEnumerator UpdateHealthBar(int oldHealth, bool animate = true)
    {
        Debug.Log("Updating Health");
        GameObject healthBar = GameObject.Find("HealthBar");
        healthBar.transform.position = transform.position;
        yield return StartCoroutine(healthBar.GetComponent<HealthBar>().UpdateHealth((int) oldHealth, (int)health, (int)maxHealth, transform.position, animate));
    }
}