using System.Collections.Generic;

namespace FirstStepsTweaks.Teleport
{
    public sealed class TpaRequestStore
    {
        private readonly Dictionary<string, List<TpaRequestRecord>> requestsByTargetUid = new Dictionary<string, List<TpaRequestRecord>>();

        public void Add(TpaRequestRecord request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TargetUid))
            {
                return;
            }

            if (!requestsByTargetUid.TryGetValue(request.TargetUid, out List<TpaRequestRecord> requests))
            {
                requests = new List<TpaRequestRecord>();
                requestsByTargetUid[request.TargetUid] = requests;
            }

            requests.Add(request);
        }

        public bool TryGetPending(string targetUid, out List<TpaRequestRecord> requests)
        {
            return requestsByTargetUid.TryGetValue(targetUid, out requests) && requests.Count > 0;
        }

        public bool TryTakeFirst(string targetUid, out TpaRequestRecord request)
        {
            request = null;
            if (!TryGetPending(targetUid, out List<TpaRequestRecord> requests))
            {
                return false;
            }

            request = requests[0];
            requests.RemoveAt(0);
            return request != null;
        }

        public bool TryCancelByRequester(string requesterUid, out TpaRequestRecord request)
        {
            request = null;
            foreach (KeyValuePair<string, List<TpaRequestRecord>> pair in requestsByTargetUid)
            {
                List<TpaRequestRecord> requests = pair.Value;
                for (int index = 0; index < requests.Count; index++)
                {
                    if (requests[index].RequesterUid == requesterUid)
                    {
                        request = requests[index];
                        requests.RemoveAt(index);
                        return true;
                    }
                }
            }

            return false;
        }

        public List<TpaRequestRecord> Clear(string targetUid)
        {
            if (!requestsByTargetUid.TryGetValue(targetUid, out List<TpaRequestRecord> requests))
            {
                return new List<TpaRequestRecord>();
            }

            requestsByTargetUid.Remove(targetUid);
            return requests;
        }

        public void Remove(string targetUid, TpaRequestRecord request)
        {
            if (request == null || !requestsByTargetUid.TryGetValue(targetUid, out List<TpaRequestRecord> requests))
            {
                return;
            }

            requests.Remove(request);
        }
    }
}
