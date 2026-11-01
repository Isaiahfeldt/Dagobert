Added identity validation around price caching and application.

- Key cached values by exact marketable item ID and HQ/NQ.
- Bind requests and responses to an active generation; ignore wrong-item marketboard batches and obsolete callbacks.
- Check the open listing before caching or applying a price; stop on mismatches or ambiguous identity.
- Clear the cache for post-pinch actions as well as main/scheduled runs.
- Preserve pending responses when waiting for the adjustment window instead of clearing them on every retry.

Build and 39 checks pass. In-game regression testing remains necessary; this addresses verified validation gaps without claiming the original incorrect-price report is fully explained.
