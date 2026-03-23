using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BallProjectilePool
{
    private readonly GameObject template;
    private readonly RectTransform container;
    private readonly List<BallProjectile> allProjectiles = new List<BallProjectile>(64);
    private readonly Stack<BallProjectile> availableProjectiles = new Stack<BallProjectile>(64);
    private readonly HashSet<BallProjectile> availableProjectileSet = new HashSet<BallProjectile>();

    public BallProjectilePool(GameObject template, RectTransform container)
    {
        this.template = template;
        this.container = container;
        EnsureTemplateHasProjectileComponent();
    }

    public void Warmup(int targetCount)
    {
        targetCount = Mathf.Max(0, targetCount);
        while (allProjectiles.Count < targetCount)
        {
            var projectile = CreateProjectile();
            if (projectile == null)
            {
                return;
            }

            availableProjectiles.Push(projectile);
        }
    }

    public BallProjectile Acquire()
    {
        while (availableProjectiles.Count > 0)
        {
            var projectile = availableProjectiles.Pop();
            availableProjectileSet.Remove(projectile);
            if (projectile != null)
            {
                return projectile;
            }
        }

        return CreateProjectile();
    }

    public void Release(BallProjectile projectile)
    {
        if (projectile == null)
        {
            return;
        }

        projectile.ReturnToPool();
        if (availableProjectileSet.Add(projectile))
        {
            availableProjectiles.Push(projectile);
        }
    }

    public void ReleaseAll()
    {
        availableProjectiles.Clear();
        availableProjectileSet.Clear();
        for (int i = 0; i < allProjectiles.Count; i++)
        {
            var projectile = allProjectiles[i];
            if (projectile == null)
            {
                continue;
            }

            projectile.ReturnToPool();
            availableProjectiles.Push(projectile);
            availableProjectileSet.Add(projectile);
        }
    }

    private BallProjectile CreateProjectile()
    {
        if (template == null || container == null)
        {
            return null;
        }

        var projectileObject = Object.Instantiate(template, container, false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        //projectileObject.name = $"Projectile {allProjectiles.Count + 1}";
#endif
        projectileObject.SetActive(false);

        var projectileCountLabel = projectileObject.transform.Find("Number");
        if (projectileCountLabel != null)
        {
            projectileCountLabel.gameObject.SetActive(false);
        }

        var projectileGraphic = projectileObject.GetComponent<Graphic>();
        if (projectileGraphic != null)
        {
            projectileGraphic.enabled = true;
            projectileGraphic.raycastTarget = false;
        }

        var projectile = projectileObject.GetComponent<BallProjectile>();
        if (projectile == null)
        {
            return null;
        }

        allProjectiles.Add(projectile);
        return projectile;
    }

    private void EnsureTemplateHasProjectileComponent()
    {
        if (template == null || template.GetComponent<BallProjectile>() != null)
        {
            return;
        }

        template.AddComponent<BallProjectile>();
    }
}
