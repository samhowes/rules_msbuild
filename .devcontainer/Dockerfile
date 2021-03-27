FROM l.gcr.io/google/bazel:latest

## runtime deps
# python3.9
RUN add-apt-repository -y ppa:deadsnakes/ppa
RUN apt update
RUN apt install -y python3.9 python3.9-distutils && rm /usr/bin/python3 && ln -s /usr/bin/python3.9 /usr/bin/python3

## devtime deps
# go 
RUN wget -c https://golang.org/dl/go1.16.2.linux-amd64.tar.gz -qO - | tar -C /usr/local -xz
ENV PATH="$PATH:/usr/local/go/bin:~/go/bin"
# bazel tools
RUN go get github.com/bazelbuild/buildtools/buildifier
RUN go get github.com/bazelbuild/buildtools/buildozer
RUN go get github.com/bazelbuild/buildtools/unused_deps


## dotnet deps
RUN apt install icu-devtools

# bazel config
RUN echo "build --sandbox_debug\n" > ~/.bazelrc
