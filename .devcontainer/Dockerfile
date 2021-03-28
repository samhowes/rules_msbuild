FROM ubuntu:latest

RUN apt-get update
RUN apt-get install -y software-properties-common sudo wget git build-essential nodejs npm

## runtime deps
# python3.9
RUN add-apt-repository -y ppa:deadsnakes/ppa
RUN apt-get update
RUN apt-get install -y python3.9 python3.9-distutils && rm /usr/bin/python3 && ln -s /usr/bin/python3.9 /usr/bin/python3

## devtime deps
# go 
RUN wget -c https://golang.org/dl/go1.16.2.linux-amd64.tar.gz -qO - | tar -C /usr/local -xz
ENV PATH="$PATH:/usr/local/go/bin:~/go/bin"
# bazel tools
RUN go get github.com/bazelbuild/buildtools/buildifier
RUN go get github.com/bazelbuild/buildtools/buildozer
RUN go get github.com/bazelbuild/buildtools/unused_deps
RUN go get golang.org/x/tools/gopls
RUN go get github.com/bazelbuild/bazelisk
ENV PATH="$PATH:$(go env GOPATH)/bin"
RUN type -P bazelisk | xargs -n 1 -I {} ln -s {} /usr/bin/bazel

## dotnet deps
RUN apt-get install -y icu-devtools

# bazel config
RUN echo "build --sandbox_debug\n" > ~/.bazelrc
