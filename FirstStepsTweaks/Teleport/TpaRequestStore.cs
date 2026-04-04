using System.Collections.Generic;

namespace FirstStepsTweaks.Teleport
{
    public sealed class TpaRequestStore
    {
        private readonly Dictionary<string, List<TpaRequestRecord>> requestsByTargetUid = new Dictionary<string, List<TpaRequestRecord>>();
        private long nextRequestSequence = 1;

        public void Add(TpaRequestRecord request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TargetUid))
            {
                return;
            }

            if (request.CreatedSequence <= 0)
            {
                request.CreatedSequence = nextRequestSequence++;
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
            if (requestsByTargetUid.TryGetValue(targetUid, out requests) && requests.Count > 0)
            {
                return true;
            }

            requests = new List<TpaRequestRecord>();
            return false;
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
            if (requests.Count == 0)
            {
                requestsByTargetUid.Remove(targetUid);
            }

            return request != null;
        }

        public bool TryCancelByRequester(string requesterUid, out TpaRequestRecord request)
        {
            request = null;
            string matchedTargetUid = null;
            int matchedIndex = -1;

            foreach (KeyValuePair<string, List<TpaRequestRecord>> pair in requestsByTargetUid)
            {
                List<TpaRequestRecord> requests = pair.Value;
                for (int index = 0; index < requests.Count; index++)
                {
                    TpaRequestRecord candidate = requests[index];
                    if (candidate.RequesterUid != requesterUid)
                    {
                        continue;
                    }

                    if (request != null && candidate.CreatedSequence >= request.CreatedSequence)
                    {
                        continue;
                    }

                    request = candidate;
                    matchedTargetUid = pair.Key;
                    matchedIndex = index;
                }
            }

            if (request == null || matchedTargetUid == null || matchedIndex < 0)
            {
                return false;
            }

            List<TpaRequestRecord> matchedRequests = requestsByTargetUid[matchedTargetUid];
            matchedRequests.RemoveAt(matchedIndex);
            if (matchedRequests.Count == 0)
            {
                requestsByTargetUid.Remove(matchedTargetUid);
            }

            return true;
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
            if (requests.Count == 0)
            {
                requestsByTargetUid.Remove(targetUid);
            }
        }
    }
}
