using System;
using DG.Tweening;
using UnityEngine;

// Attach this to VisualRoot, so the animation survives destruction of the Item.
public sealed class ChipPocketPresentation : MonoBehaviour
{
    [Header("Prefab parts")]
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform caseTransform;
    [SerializeField] private Transform rightLid;
    [SerializeField] private Transform chip01;
    [SerializeField] private Transform chip02;
    [SerializeField] private Transform chip03;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float liftHeight = 0.015f;
    [SerializeField, Min(0f)] private float liftDuration = 0.085f;
    [SerializeField, Min(1f)] private float liftScale = 1.015f;
    [SerializeField] private Vector3 lidPopOffset = new Vector3(-0.09f, 0.025f, 0.01f);
    [SerializeField] private Vector3 lidPopEulerAngles = new Vector3(0f, -6f, -7f);
    [SerializeField, Min(0f)] private float lidPopDuration = 0.135f;
    [SerializeField, Min(0f)] private float afterLidPopDelay = 0.055f;
    [SerializeField] private Vector3 chipLaunchOffset = new Vector3(0f, 0.115f, 0.018f);
    [SerializeField, Min(0f)] private float chipLaunchDuration = 0.09f;
    [SerializeField, Min(0f)] private float chipJumpPower = 0.028f;
    [SerializeField, Min(0f)] private float chipMoveDuration = 0.43f;
    [SerializeField, Min(0f)] private float chipStagger = 0.075f;
    [SerializeField, Min(0f)] private float consumeDuration = 0.11f;

    private Sequence sequence;
    private Action syncChips;
    private Action finished;
    private bool synced;
    private bool completed;

    public bool TryPlay(Vector3[] targets, Action onChipsArrived, Action onFinished)
    {
        if (sequence != null || completed || !isActiveAndEnabled ||
            visualRoot != transform || caseTransform == null || rightLid == null ||
            chip01 == null || chip02 == null || chip03 == null ||
            targets == null || targets.Length != 3)
        {
            return false;
        }

        syncChips = onChipsArrived;
        finished = onFinished;
        visualRoot.SetParent(null, true);

        try
        {
            Vector3 startPosition = visualRoot.position;
            Vector3 startScale = visualRoot.localScale;
            Vector3 closedLidPosition = rightLid.localPosition;
            Quaternion closedLidRotation = rightLid.localRotation;
            Quaternion poppedLidRotation = closedLidRotation *
                Quaternion.Euler(lidPopEulerAngles);

            sequence = DOTween.Sequence().SetAutoKill(true);
            sequence.Append(visualRoot.DOMoveY(
                startPosition.y + liftHeight, liftDuration).SetEase(Ease.OutCubic));
            sequence.Join(visualRoot.DOScale(
                startScale * liftScale, liftDuration).SetEase(Ease.OutCubic));
            sequence.Append(rightLid.DOLocalMove(
                closedLidPosition + lidPopOffset, lidPopDuration)
                .SetEase(Ease.OutQuart));
            sequence.Join(rightLid.DOLocalRotateQuaternion(
                poppedLidRotation, lidPopDuration).SetEase(Ease.OutQuart));
            sequence.AppendCallback(() => rightLid.SetParent(null, true));
            sequence.AppendInterval(afterLidPopDelay);

            Transform[] chips = { chip01, chip02, chip03 };
            float firstLaunch = sequence.Duration();
            for (int index = 0; index < chips.Length; index++)
            {
                sequence.Insert(firstLaunch + index * chipStagger,
                    CreateChipFlight(chips[index], targets[index]));
            }

            sequence.AppendCallback(SyncOnce);
            sequence.Append(visualRoot.DOScale(
                Vector3.zero, consumeDuration).SetEase(Ease.InCubic));
            sequence.Join(rightLid.DOScale(
                Vector3.zero, consumeDuration).SetEase(Ease.InCubic));
            sequence.OnComplete(Finish);
            sequence.OnKill(Finish);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            Finish();
            return false;
        }
    }

    public void Cancel()
    {
        if (completed)
        {
            return;
        }

        if (sequence != null && sequence.IsActive())
        {
            sequence.Kill(false);
        }
        Finish();
    }

    private Sequence CreateChipFlight(Transform chip, Vector3 destination)
    {
        Sequence flight = DOTween.Sequence();
        flight.AppendCallback(() =>
        {
            if (chip != null)
            {
                chip.SetParent(null, true);
            }
        });
        flight.Append(chip.DOMove(
                transform.TransformDirection(chipLaunchOffset), chipLaunchDuration)
            .SetRelative().SetEase(Ease.OutQuart));
        flight.Append(chip.DOJump(
                destination, chipJumpPower, 1, chipMoveDuration)
            .SetEase(Ease.InOutCubic));
        return flight;
    }

    private void SyncOnce()
    {
        if (synced)
        {
            return;
        }

        if (chip01 != null) chip01.gameObject.SetActive(false);
        if (chip02 != null) chip02.gameObject.SetActive(false);
        if (chip03 != null) chip03.gameObject.SetActive(false);
        try
        {
            syncChips?.Invoke();
            synced = true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private void Finish()
    {
        if (completed)
        {
            return;
        }

        completed = true;
        SyncOnce();
        try
        {
            finished?.Invoke();
        }
        finally
        {
            DestroyPart(chip01);
            DestroyPart(chip02);
            DestroyPart(chip03);
            DestroyPart(rightLid);
            DestroyPart(visualRoot);
        }
    }

    private static void DestroyPart(Transform part)
    {
        if (part == null)
        {
            return;
        }
        if (Application.isPlaying) Destroy(part.gameObject);
        else DestroyImmediate(part.gameObject);
    }

    private void OnDisable()
    {
        if (!completed && sequence != null)
        {
            Cancel();
        }
    }
}
