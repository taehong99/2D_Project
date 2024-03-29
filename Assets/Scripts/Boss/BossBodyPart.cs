using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Parent class for boss body parts that share common movement coroutines
public class BossBodyPart : MonoBehaviour
{
    protected virtual IEnumerator LerpToDestination(Transform transform, Vector2 destination, float speed)
    {
        float t = 0;
        Vector2 startPos = transform.localPosition;
        Vector2 endPos = destination;
        while (t < 1)
        {
            transform.localPosition = Vector2.Lerp(startPos, endPos, t);
            t += Time.deltaTime * speed;
            yield return null;
        }
    }

    protected virtual IEnumerator LerpWithOffset(Transform transform, Vector2 offset, float speed)
    {
        float t = 0;
        Vector2 startPos = transform.localPosition;
        Vector2 endPos = startPos;
        endPos += offset;
        while (t < 1)
        {
            transform.localPosition = Vector2.Lerp(startPos, endPos, t);
            t += Time.deltaTime * speed;
            yield return null;
        }
    }

    protected virtual IEnumerator TiltToDestination(Transform transform, Vector2 targetPos, Quaternion targetRot, float speed)
    {
        float t = 0;
        transform.GetPositionAndRotation(out Vector3 startPos, out Quaternion startRot);
        while (t < 1)
        {
            transform.localPosition = Vector2.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Lerp(startRot, targetRot, t);
            t += Time.deltaTime * speed;
            yield return null;
        }
    }
}