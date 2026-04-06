using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared.Body.Prototypes;
using Content.Shared.Body.Part;
using Content.Shared.Humanoid;
using Content.Shared._Shitmed.Body.Events;
using Content.Shared._Shitmed.Body.Part;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Shared.Body.Systems;

public partial class SharedBodySystem
{
    private void InitializeBody()
    {
        SubscribeLocalEvent<BodyComponent, MapInitEvent>(OnCompatBodyMapInit);
    }

    private void InitializeParts()
    {
    }

    private void InitializeOrgans()
    {
    }

    private void OnCompatBodyMapInit(Entity<BodyComponent> ent, ref MapInitEvent args)
    {
        EnsureBodyPartTree(ent, ent.Comp);
    }

    private bool EnsureBodyPartTree(EntityUid bodyId, BodyComponent? body = null)
    {
        if (!Resolve(bodyId, ref body, logMissing: false))
            return false;

        body.RootContainer ??= Containers.EnsureContainer<ContainerSlot>(bodyId, BodyRootContainerId);

        if (body.RootContainer.ContainedEntity is not null)
            return true;

        if (!TryGetBodyPrototype(bodyId, body, out var prototype))
            return false;

        var rootSlot = prototype.Root;
        if (!prototype.Slots.TryGetValue(rootSlot, out var rootData) || rootData.Part is null)
            return false;

        var coords = new EntityCoordinates(bodyId, Vector2.Zero);
        var rootPartId = Spawn(rootData.Part.Value, coords);

        if (!TryComp(rootPartId, out BodyPartComponent? rootPart)
            || !Containers.Insert(rootPartId, body.RootContainer))
        {
            QueueDel(rootPartId);
            return false;
        }

        rootPart.Body = bodyId;
        Dirty(rootPartId, rootPart);
        EnsureOrganSlots(rootPartId, rootPart, rootData.Organs);
        RaiseInitialPartAdded(bodyId, rootSlot, rootPartId, rootPart);

        var frontier = new Queue<string>();
        frontier.Enqueue(rootSlot);

        var visited = new HashSet<string> { rootSlot };
        var partEntities = new Dictionary<string, EntityUid>
        {
            [rootSlot] = rootPartId
        };

        while (frontier.TryDequeue(out var currentSlot))
        {
            if (!prototype.Slots.TryGetValue(currentSlot, out var currentData))
                continue;

            var parentPartId = partEntities[currentSlot];
            if (!TryComp(parentPartId, out BodyPartComponent? parentPart))
                continue;

            IEnumerable<string> connections = currentData.Connections is not null
                ? currentData.Connections
                : Array.Empty<string>();
            foreach (var connection in connections)
            {
                if (!visited.Add(connection)
                    || !prototype.Slots.TryGetValue(connection, out var connectionData)
                    || connectionData.Part is null)
                {
                    continue;
                }

                var childPartId = Spawn(connectionData.Part.Value, new EntityCoordinates(parentPartId, Vector2.Zero));
                if (!TryComp(childPartId, out BodyPartComponent? childPart))
                {
                    QueueDel(childPartId);
                    continue;
                }

                if (!TryCreatePartSlot(parentPartId, connection, childPart.PartType, out _, parentPart))
                {
                    QueueDel(childPartId);
                    continue;
                }

                childPart.Body = bodyId;
                Dirty(childPartId, childPart);

                if (!AttachPart(parentPartId, connection, childPartId, parentPart, childPart, raiseEvents: false))
                {
                    QueueDel(childPartId);
                    continue;
                }

                EnsureOrganSlots(childPartId, childPart, connectionData.Organs);
                RaiseInitialPartAdded(bodyId, connection, childPartId, childPart);
                partEntities[connection] = childPartId;
                frontier.Enqueue(connection);
            }
        }

        return true;
    }

    private void RaiseInitialPartAdded(EntityUid bodyId, string slotId, EntityUid partId, BodyPartComponent part)
    {
        EnsureComp<BodyPartAppearanceComponent>(partId);

        if (!TryComp(bodyId, out BodyComponent? body))
            return;

        var ev = new BodyPartAddedEvent(slotId, (partId, part));
        RaiseLocalEvent(bodyId, ref ev);

        var modify = new BodyPartComponentsModifyEvent(bodyId, true);
        RaiseLocalEvent(partId, modify);
    }

    private bool TryGetBodyPrototype(EntityUid bodyId, BodyComponent body, [NotNullWhen(true)] out BodyPrototype? prototype)
    {
        prototype = null;

        if (body.Prototype is { } bodyProtoId && Prototypes.TryIndex(bodyProtoId, out prototype))
            return true;

        if (TryComp<HumanoidProfileComponent>(bodyId, out var humanoid))
        {
            var speciesId = humanoid.Species.ToString();
            if (Prototypes.TryIndex<BodyPrototype>(speciesId, out prototype))
            {
                body.Prototype = speciesId;
                Dirty(bodyId, body);
                return true;
            }
        }

        return false;
    }

    private void EnsureOrganSlots(EntityUid partId, BodyPartComponent part, Dictionary<string, string>? organs)
    {
        if (organs is null)
            return;

        foreach (var (slotId, organProto) in organs)
        {
            TryCreateOrganSlot(partId, slotId, out _, part);
            var organId = Spawn(organProto, new EntityCoordinates(partId, Vector2.Zero));
            InsertOrgan(partId, organId, slotId, part);
        }
    }

    private bool TryCreateOrganSlot(
        EntityUid? partId,
        string slotId,
        [NotNullWhen(true)] out OrganSlot? slot,
        BodyPartComponent? part = null)
    {
        slot = null;

        if (partId is null || !Resolve(partId.Value, ref part, logMissing: false))
            return false;

        Containers.EnsureContainer<ContainerSlot>(partId.Value, GetOrganContainerId(slotId));
        slot = new OrganSlot(slotId);

        if (!part.Organs.ContainsKey(slotId) && !part.Organs.TryAdd(slotId, slot.Value))
            return false;

        Dirty(partId.Value, part);
        return true;
    }

    public (EntityUid Entity, BodyPartComponent BodyPart)? GetRootPartOrNull(EntityUid bodyId, BodyComponent? body = null)
    {
        EnsureBodyPartTree(bodyId, body);

        if (!Resolve(bodyId, ref body, logMissing: false)
            || body.RootContainer is null
            || body.RootContainer.ContainedEntity is null)
            return null;

        var root = body.RootContainer.ContainedEntity.Value;
        return TryComp(root, out BodyPartComponent? part) ? (root, part) : null;
    }

    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyPartChildren(EntityUid partId, BodyPartComponent? part = null)
    {
        if (!Resolve(partId, ref part, logMissing: false))
            yield break;

        yield return (partId, part);

        foreach (var slotId in part.Children.Keys)
        {
            if (!Containers.TryGetContainer(partId, GetPartSlotContainerId(slotId), out var container))
                continue;

            foreach (var childUid in container.ContainedEntities)
            {
                if (!TryComp(childUid, out BodyPartComponent? childPart))
                    continue;

                foreach (var value in GetBodyPartChildren(childUid, childPart))
                {
                    yield return value;
                }
            }
        }
    }

    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildren(
        EntityUid? bodyId,
        BodyComponent? body = null,
        BodyPartComponent? rootPart = null)
    {
        if (bodyId is not null)
            EnsureBodyPartTree(bodyId.Value, body);

        if (bodyId is null
            || !Resolve(bodyId.Value, ref body, logMissing: false)
            || body.RootContainer is null
            || body.RootContainer.ContainedEntity is null
            || !Resolve(body.RootContainer.ContainedEntity.Value, ref rootPart, logMissing: false))
        {
            yield break;
        }

        foreach (var child in GetBodyPartChildren(body.RootContainer.ContainedEntity.Value, rootPart))
        {
            yield return child;
        }
    }

    /// <summary>
    /// Internal body parts live inside the body's containers and are not valid world interaction targets.
    /// Systems that need a normal SS14 interaction target should use the body entity while keeping the
    /// selected body-part entity for surgery/body logic.
    /// </summary>
    public EntityUid GetInteractionTarget(EntityUid bodyId, EntityUid? bodyPartId = null)
    {
        if (bodyPartId is null || bodyPartId == bodyId)
            return bodyId;

        return TryComp(bodyPartId.Value, out BodyPartComponent? part) && part.Body == bodyId
            ? bodyId
            : bodyPartId.Value;
    }

    public IEnumerable<(EntityUid Id, OrganComponent Component)> GetPartOrgans(EntityUid partId, BodyPartComponent? part = null)
    {
        if (!Resolve(partId, ref part, logMissing: false))
            yield break;

        var yielded = new HashSet<EntityUid>();

        foreach (var slotId in part.Organs.Keys)
        {
            if (!Containers.TryGetContainer(partId, GetOrganContainerId(slotId), out var container))
                continue;

            foreach (var organUid in container.ContainedEntities)
            {
                if (TryComp(organUid, out OrganComponent? organ))
                {
                    yielded.Add(organUid);
                    yield return (organUid, organ);
                }
            }
        }

        if (part.Body is not { } bodyId
            || !TryComp(bodyId, out BodyComponent? body)
            || body.Organs is null
            || !TryGetBodyPrototype(bodyId, body, out var prototype))
        {
            yield break;
        }

        var partSlotId = GetSlotFromBodyPart(part);
        if (!prototype.Slots.TryGetValue(partSlotId, out var bodySlot))
            yield break;

        foreach (var organUid in body.Organs.ContainedEntities)
        {
            if (yielded.Contains(organUid)
                || !TryComp(organUid, out OrganComponent? organ)
                || !OrganBelongsToSlot(organ, bodySlot))
            {
                continue;
            }

            yield return (organUid, organ);
        }
    }

    public IEnumerable<(EntityUid Id, OrganComponent Component)> GetBodyOrgans(EntityUid bodyId, BodyComponent? body = null)
    {
        var yielded = new HashSet<EntityUid>();

        foreach (var part in GetBodyChildren(bodyId, body))
        {
            foreach (var organ in GetPartOrgans(part.Id, part.Component))
            {
                if (!yielded.Add(organ.Id))
                    continue;

                yield return organ;
            }
        }

        if (!Resolve(bodyId, ref body, logMissing: false) || body.Organs is null)
            yield break;

        foreach (var organUid in body.Organs.ContainedEntities)
        {
            if (!yielded.Contains(organUid) && TryComp(organUid, out OrganComponent? organ))
                yield return (organUid, organ);
        }
    }

    public IEnumerable<(EntityUid Id, BodyPartComponent Component)> GetBodyChildrenOfType(
        EntityUid bodyId,
        BodyPartType type,
        BodyComponent? body = null,
        BodyPartSymmetry? symmetry = null)
    {
        foreach (var part in GetBodyChildren(bodyId, body))
        {
            if (part.Component.PartType == type && (symmetry == null || part.Component.Symmetry == symmetry))
                yield return part;
        }
    }

    public int GetBodyPartCount(EntityUid bodyId, BodyPartType partType, BodyComponent? body = null)
    {
        var count = 0;
        foreach (var part in GetBodyChildrenOfType(bodyId, partType, body))
        {
            count++;
        }

        return count;
    }

    public bool TryGetBodyPartOrgans(
        EntityUid uid,
        Type type,
        [NotNullWhen(true)] out List<(EntityUid Id, OrganComponent Organ)>? organs,
        BodyPartComponent? part = null)
    {
        if (!Resolve(uid, ref part, logMissing: false))
        {
            organs = null;
            return false;
        }

        var list = new List<(EntityUid Id, OrganComponent Organ)>();
        foreach (var organ in GetPartOrgans(uid, part))
        {
            if (HasComp(organ.Id, type))
                list.Add((organ.Id, organ.Component));
        }

        organs = list.Count > 0 ? list : null;
        return organs != null;
    }

    public bool TryGetBodyOrganEntityComps<TComp>(
        EntityUid bodyId,
        [NotNullWhen(true)] out List<Entity<TComp>>? entities,
        BodyComponent? body = null)
        where TComp : IComponent
    {
        var results = new List<Entity<TComp>>();

        foreach (var organ in GetBodyOrgans(bodyId, body))
        {
            if (TryComp<TComp>(organ.Id, out var comp))
                results.Add((organ.Id, comp));
        }

        entities = results.Count > 0 ? results : null;
        return entities != null;
    }

    public bool TrySetOrganUsed(EntityUid organId, bool used, OrganComponent? organ = null)
    {
        if (!Resolve(organId, ref organ, logMissing: false) || organ.Used == used)
            return false;

        organ.Used = used;
        Dirty(organId, organ);
        return true;
    }

    public bool CanAttachToSlot(EntityUid parentId, string slotId, BodyPartComponent? parentPart = null)
    {
        return Resolve(parentId, ref parentPart, logMissing: false) && parentPart.Children.ContainsKey(slotId);
    }

    public bool IsPartSlotEmpty(EntityUid parentId, string slotId, BodyPartComponent? parentPart = null)
    {
        if (!Resolve(parentId, ref parentPart, logMissing: false)
            || !parentPart.Children.ContainsKey(slotId)
            || !Containers.TryGetContainer(parentId, GetPartSlotContainerId(slotId), out var container))
        {
            return false;
        }

        return container.ContainedEntities.Count == 0;
    }

    public bool TryCreatePartSlot(
        EntityUid? partId,
        string slotId,
        BodyPartType partType,
        [NotNullWhen(true)] out BodyPartSlot? slot,
        BodyPartComponent? part = null)
    {
        slot = null;

        if (partId is null || !Resolve(partId.Value, ref part, logMissing: false))
            return false;

        Containers.EnsureContainer<ContainerSlot>(partId.Value, GetPartSlotContainerId(slotId));
        slot = new BodyPartSlot(slotId, partType);

        if (!part.Children.ContainsKey(slotId) && !part.Children.TryAdd(slotId, slot.Value))
            return false;

        Dirty(partId.Value, part);
        return true;
    }

    public bool AttachPart(
        EntityUid parentPartId,
        string slotId,
        EntityUid partId,
        BodyPartComponent? parentPart = null,
        BodyPartComponent? part = null,
        bool raiseEvents = true)
    {
        if (!Resolve(parentPartId, ref parentPart, logMissing: false)
            || !Resolve(partId, ref part, logMissing: false)
            || !parentPart.Children.TryGetValue(slotId, out var slot)
            || part.PartType != slot.Type
            || !Containers.TryGetContainer(parentPartId, GetPartSlotContainerId(slot.Id), out var container)
            || !Containers.CanInsert(partId, container))
        {
            return false;
        }

        part.ParentSlot = slot;
        Dirty(partId, part);
        if (!Containers.Insert(partId, container))
            return false;

        if (raiseEvents && part.Body is { } bodyId)
        {
            var added = new BodyPartAddedEvent(slotId, (partId, part));
            RaiseLocalEvent(bodyId, ref added);

            var modify = new BodyPartComponentsModifyEvent(bodyId, true);
            RaiseLocalEvent(partId, modify);
        }

        return true;
    }

    public bool InsertOrgan(
        EntityUid partId,
        EntityUid organId,
        string slotId,
        BodyPartComponent? part = null,
        OrganComponent? organ = null)
    {
        if (!Resolve(partId, ref part, logMissing: false)
            || !Resolve(organId, ref organ, logMissing: false)
            || part.Body is not { } bodyId
            || !TryComp(bodyId, out BodyComponent? body)
            || body.Organs is null)
        {
            return false;
        }

        if (string.IsNullOrEmpty(organ.SlotId))
        {
            organ.SlotId = slotId;
            Dirty(organId, organ);
        }

        return Containers.Insert(organId, body.Organs);
    }

    public bool RemoveOrgan(EntityUid organId, OrganComponent? organ = null)
    {
        if (!Containers.TryGetContainingContainer((organId, null, null), out var container))
            return false;

        return Containers.Remove(organId, container);
    }

    public string GetSlotFromBodyPart(BodyPartComponent? part)
    {
        if (part is null)
            return string.Empty;

        var slotName = string.IsNullOrEmpty(part.SlotId) ? part.PartType.ToString().ToLowerInvariant() : part.SlotId;
        return part.Symmetry != BodyPartSymmetry.None
            ? $"{part.Symmetry.ToString().ToLowerInvariant()} {slotName}"
            : slotName;
    }

    private static bool OrganBelongsToSlot(OrganComponent organ, BodyPrototypeSlot bodySlot)
    {
        var organSlotId = NormalizeSlotName(string.IsNullOrWhiteSpace(organ.SlotId)
            ? organ.Category?.Id
            : organ.SlotId);

        if (string.IsNullOrEmpty(organSlotId))
            return false;

        IEnumerable<string> keys = bodySlot.Organs is not null
            ? bodySlot.Organs.Keys
            : Array.Empty<string>();
        return keys.Any(key => NormalizeSlotName(key) == organSlotId);
    }

    private static string NormalizeSlotName(string? id)
    {
        return string.IsNullOrWhiteSpace(id)
            ? string.Empty
            : id.Trim().Replace("_", " ").ToLowerInvariant();
    }

    public (EntityUid Part, BodyPartComponent Component, BodyPartSlot Slot)? GetParentPartAndSlotOrNull(
        EntityUid partId,
        BodyPartComponent? part = null)
    {
        if (!Resolve(partId, ref part, logMissing: false)
            || part.Body is not { } bodyId)
        {
            return null;
        }

        foreach (var candidate in GetBodyChildren(bodyId))
        {
            foreach (var slot in candidate.Component.Children.Values)
            {
                if (!Containers.TryGetContainer(candidate.Id, GetPartSlotContainerId(slot.Id), out var container)
                    || !container.ContainedEntities.Contains(partId))
                {
                    continue;
                }

                return (candidate.Id, candidate.Component, slot);
            }
        }

        return null;
    }

    public bool DropSlotContents(EntityUid uid, string slotId)
    {
        if (!Containers.TryGetContainer(uid, GetPartSlotContainerId(slotId), out var partContainer))
            return false;

        var changed = false;
        foreach (var entity in partContainer.ContainedEntities.ToList())
        {
            if (Containers.Remove(entity, partContainer))
            {
                SharedTransform.DropNextTo(entity, uid);
                changed = true;
            }
        }

        if (Containers.TryGetContainer(uid, GetOrganContainerId(slotId), out var organContainer))
        {
            foreach (var entity in organContainer.ContainedEntities.ToList())
            {
                if (Containers.Remove(entity, organContainer))
                {
                    SharedTransform.DropNextTo(entity, uid);
                    changed = true;
                }
            }
        }

        return changed;
    }

    public bool DropPart(EntityUid partId, BodyPartComponent? part = null)
    {
        if (!Resolve(partId, ref part, logMissing: false))
            return false;

        var parent = GetParentPartAndSlotOrNull(partId, part);
        if (parent is null)
            return false;

        if (!Containers.TryGetContainer(parent.Value.Part, GetPartSlotContainerId(parent.Value.Slot.Id), out var container)
            || !Containers.Remove(partId, container))
        {
            return false;
        }

        if (part.Body is { } bodyId)
        {
            var removed = new BodyPartRemovedEvent(parent.Value.Slot.Id, (partId, part));
            RaiseLocalEvent(bodyId, ref removed);

            var modify = new BodyPartComponentsModifyEvent(bodyId, false);
            RaiseLocalEvent(partId, modify);
        }

        SharedTransform.DropNextTo(partId, parent.Value.Part);
        return true;
    }
}
