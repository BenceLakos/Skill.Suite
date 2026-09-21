# The DNS server the venue LAN points at. Built here rather than pulled, because there is no official dnsmasq
# image and the maintained third-party ones (dockurr/dnsmasq and friends) wrap the binary in an entrypoint
# script that never passes the container command through to it - and the command is precisely where this
# stack's ${DOMAIN} and ${HOST_LAN_IP} have to land, since compose interpolates `command:` but a dnsmasq
# configuration FILE cannot expand a variable at all. Three lines on alpine keep every setting visible in
# infra/compose.yaml and put the binary on the same patch channel as every other alpine package.
FROM alpine:3.22

# No version pin on the package itself: the alpine tag above already fixes the dnsmasq minor (2.91), and the
# revisions alpine publishes inside a release ARE the security fixes - pinning one makes this build fail the
# first time one lands. Bump the tag above to move to a newer dnsmasq.
#
# dnsmasq drops to its own unprivileged user after binding, which it can do with docker's default capability
# set; nothing here needs NET_ADMIN (that is the DHCP server, which this image never runs).
RUN apk add --no-cache dnsmasq

# The entrypoint is the bare binary: everything this server does is a flag in infra/compose.yaml, including
# --conf-file=/dev/null, which is what stops the alpine package's own /etc/dnsmasq.conf from contributing
# settings that are not written down there.
ENTRYPOINT ["dnsmasq"]
