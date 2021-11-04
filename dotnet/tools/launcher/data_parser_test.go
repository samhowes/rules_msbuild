package main

import (
	"encoding/binary"
	"fmt"
	"io/ioutil"
	"os"
	"reflect"
	"testing"

	"github.com/bazelbuild/rules_go/go/tools/bazel"
)

func TestLauncher(t *testing.T) {
	dataFile, err := ioutil.TempFile(bazel.TestTmpDir(), "launchData.*.txt")
	if err != nil {
		t.Fatal(err)
	}
	defer os.Remove(dataFile.Name())

	for i := 0; i < 10; i++ {
		_, err = dataFile.WriteString("as;ldkfjasdlfkasdf;la;l3phoavoa=a;lkajsdf;ljaf01==")
		if err != nil {
			t.Fatal(err)
		}
	}

	_, err = dataFile.WriteString("dont=readme;")
	if err != nil {
		t.Fatal(err)
	}

	launchData := map[string]string{"foo": "bar", "bam": "baz"}

	launchDataSize := int64(0)
	for k, v := range launchData {
		written, _ := dataFile.WriteString(fmt.Sprintf("%s=%s\x00", k, v))
		launchDataSize += int64(written)
	}
	b := make([]byte, reflect.TypeOf(launchDataSize).Size())
	binary.LittleEndian.PutUint64(b, uint64(launchDataSize))
	_, err = dataFile.Write(b)
	if err != nil {
		t.Fatal(err)
	}

	t.Logf("Launch file written to: %s", dataFile.Name())

	launchInfo, err := GetLaunchInfo(dataFile.Name())
	if err != nil {
		t.Fatal(err)
	}

	if launchInfo.Data["foo"] != "bar" {
		t.Errorf("Deserialized the wrong value for 'foo': %s", launchInfo.Data["foo"])
	}
}
