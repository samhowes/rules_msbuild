package BuildProperties

import (
	"github.com/stretchr/testify/assert"
	"os"
	"testing"
)

func TestBuildProperties(t *testing.T) {
	projectFileContents, err := os.ReadFile("BuildProperties.csproj")
	assert.NoError(t, err)

	assert.Contains(t, string(projectFileContents), "<Foo>Bar</Foo>")
}
